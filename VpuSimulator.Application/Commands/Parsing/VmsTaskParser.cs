using System.Text.Json;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// Parse task_list của add-task và từng Task (SRS 3.1.3, 3.1.4). Hàm thuần, không ghi log, không ném exception với dữ liệu xấu.
///
/// Quy tắc theo SRS:
/// - Tên trường không phân biệt hoa/thường; trường lạ ở mọi cấp bị bỏ qua (IF-CMD-1, IF-CMD-5).
/// - Phân biệt vắng mặt với rỗng/0 (IF-TASK-1).
/// - Rule có name rỗng hoặc không hỗ trợ: bỏ riêng rule đó, task và các rule khác vẫn chạy (IF-RULE-1).
/// - Rule sai kiểu dữ liệu: cả task bị từ chối (IF-RULE-2).
///
/// Bốn chỗ SRS im lặng, đang chọn cách an toàn nhất, cần nhóm xác nhận (xem ghi chú trong từng chỗ):
/// (a) task thiếu task (id); (b) rule thiếu id; (c) hai rule trùng cả id lẫn name; (d) trường cấp task sai kiểu (ví dụ url là số).
/// </summary>
public static class VmsTaskParser
{
    public static AddTaskParseResult ParseAddTask(JsonElement detail, Func<string, bool>? isSupportedRule = null)
    {
        JsonProps.GetString(detail, "appId", out var appId);

        if (!JsonProps.TryGet(detail, "task_list", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return new AddTaskParseResult(null, appId, "detail.task_list vắng mặt hoặc không phải mảng");
        }

        var results = new List<TaskParseResult>();
        foreach (var item in list.EnumerateArray())
        {
            results.Add(ParseTask(item, isSupportedRule));
        }

        return new AddTaskParseResult(results, appId, null);
    }

    /// <param name="isSupportedRule">Tên rule có được hỗ trợ không. null = chấp nhận mọi tên không rỗng.</param>
    public static TaskParseResult ParseTask(JsonElement element, Func<string, bool>? isSupportedRule = null)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Rejected(null, "phần tử của task_list không phải object");
        }

        // (a) Không có task id thì không định danh được task, cũng không có uuid để phát status failed → từ chối, taskId = null.
        var idRead = JsonProps.GetString(element, "task", out var taskId);
        if (idRead != ReadResult.Ok || string.IsNullOrWhiteSpace(taskId))
        {
            return Rejected(null, "trường 'task' (task uuid) vắng mặt, rỗng hoặc sai kiểu");
        }

        var warnings = new List<string>();

        // (d) Trường cấp task sai kiểu: coi như vắng mặt kèm cảnh báo, không từ chối task.
        string? Str(string name)
        {
            if (JsonProps.GetString(element, name, out var v) == ReadResult.WrongType) warnings.Add($"'{name}' sai kiểu (cần chuỗi), bỏ qua");
            return v;
        }

        bool? Bool(string name)
        {
            if (JsonProps.GetBool(element, name, out var v) == ReadResult.WrongType) warnings.Add($"'{name}' sai kiểu (cần true/false), bỏ qua");
            return v;
        }

        int? Int(JsonElement obj, string name, string path)
        {
            if (JsonProps.GetInt(obj, name, out var v) == ReadResult.WrongType) warnings.Add($"'{path}{name}' sai kiểu (cần số nguyên), bỏ qua");
            return v;
        }

        var sourceInfo = ReadSourceInfo(element, warnings);
        var capability = JsonProps.TryGet(element, "capability", out var cap) && cap.ValueKind == JsonValueKind.Object
            ? new VmsCapability(Int(cap, "width", "capability."), Int(cap, "height", "capability."), Int(cap, "fps", "capability."), Int(cap, "out_fps", "capability."))
            : null;
        var detectors = ReadDetectors(element, warnings);

        // Rule
        var rules = new List<ParsedRule>();
        var skippedRules = new List<SkippedRule>();
        var ruleConfPresent = false;
        if (JsonProps.TryGet(element, "rule_conf", out var ruleConf) && ruleConf.ValueKind != JsonValueKind.Null)
        {
            ruleConfPresent = true;
            if (ruleConf.ValueKind != JsonValueKind.Array)
            {
                return Rejected(taskId, "'rule_conf' không phải mảng");
            }

            var seen = new HashSet<RuleKey>();
            var index = 0;
            foreach (var ruleElement in ruleConf.EnumerateArray())
            {
                var outcome = ParseRule(ruleElement, index, isSupportedRule, out var rule, out var skipped, out var message);
                switch (outcome)
                {
                    case RuleOutcome.Fail:
                        return Rejected(taskId, message);   // IF-RULE-2
                    case RuleOutcome.Skip:
                        warnings.Add(message);               // IF-RULE-1 và (b)
                        skippedRules.Add(skipped!);
                        break;
                    default:
                        // (c) Trùng cả (id, name): giữ rule đầu, bỏ rule sau kèm cảnh báo, thay vì ghi đè im lặng.
                        if (!seen.Add(rule!.Key))
                        {
                            warnings.Add($"rule_conf[{index}] ('{rule.Name}', id {rule.RoiId}) trùng (id, name) với một rule trước đó, bỏ qua");
                            skippedRules.Add(new SkippedRule(index, rule.Name, RuleSkipReason.Duplicate));
                        }
                        else
                        {
                            rules.Add(rule);
                        }

                        break;
                }

                index++;
            }
        }

        var parsed = new ParsedTask(
            TaskId: taskId,
            Enable: Bool("enable"),
            TaskType: Str("task_type"),
            StreamUuid: Str("uuid"),
            Url: Str("url"),
            UrlDis: Str("url_dis"),
            Mode: Str("mode"),
            StartTime: Str("start_time"),
            EndTime: Str("end_time"),
            TotalSegment: Int(element, "total_segment", ""),
            CurrentSegment: Int(element, "current_segment", ""),
            LastSegment: Bool("last_segment"),
            SourceInfo: sourceInfo,
            Capability: capability,
            Detectors: detectors,
            RuleConfPresent: ruleConfPresent,
            Rules: rules,
            SkippedRules: skippedRules,
            Warnings: warnings,
            Raw: element.Clone());

        return new TaskParseResult(parsed, taskId, null);
    }

    private enum RuleOutcome
    {
        Ok,
        Skip,
        Fail,
    }

    private static RuleOutcome ParseRule(
        JsonElement element,
        int index,
        Func<string, bool>? isSupportedRule,
        out ParsedRule? rule,
        out SkippedRule? skipped,
        out string message)
    {
        rule = null;
        skipped = null;
        message = string.Empty;
        var path = $"rule_conf[{index}]";

        if (element.ValueKind != JsonValueKind.Object)
        {
            message = $"{path} không phải object";
            return RuleOutcome.Fail;
        }

        // Đọc và kiểm tra kiểu MỌI trường đã biết trước, rồi mới quyết định bỏ rule hay không:
        // một rule sai kiểu làm cả task bị từ chối, kể cả khi rule đó lẽ ra chỉ bị bỏ vì tên rỗng.
        var id = JsonProps.GetInt(element, "id", out var roiId);
        var type = JsonProps.GetInt(element, "type", out var typeValue);
        var flow = JsonProps.GetInt(element, "aiFlowCode", out var aiFlowCode);
        var enable = JsonProps.GetBool(element, "enable", out var enableValue);
        var nameRead = JsonProps.GetString(element, "name", out var name);

        foreach (var (field, read, expected) in new[]
                 {
                     ("id", id, "số nguyên"), ("type", type, "số nguyên"), ("aiFlowCode", flow, "số nguyên"),
                     ("enable", enable, "true/false"), ("name", nameRead, "chuỗi"),
                 })
        {
            if (read == ReadResult.WrongType)
            {
                message = $"{path}.{field} sai kiểu dữ liệu (cần {expected})";
                return RuleOutcome.Fail;
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            message = $"{path}: 'name' vắng mặt hoặc rỗng, bỏ rule này";
            skipped = new SkippedRule(index, name, RuleSkipReason.MissingName);
            return RuleOutcome.Skip;
        }

        if (isSupportedRule is not null && !isSupportedRule(name))
        {
            message = $"{path}: rule '{name}' chưa được hỗ trợ, bỏ rule này";
            skipped = new SkippedRule(index, name, RuleSkipReason.Unsupported);
            return RuleOutcome.Skip;
        }

        // (b) Không có id thì không có roi_id để đưa vào sự kiện và không tạo được khóa unit → bỏ rule này.
        if (id != ReadResult.Ok)
        {
            message = $"{path} ('{name}'): thiếu 'id', bỏ rule này";
            skipped = new SkippedRule(index, name, RuleSkipReason.MissingId);
            return RuleOutcome.Skip;
        }

        rule = new ParsedRule(roiId!.Value, name, typeValue, aiFlowCode, enableValue, element.Clone());
        return RuleOutcome.Ok;
    }

    private static VmsSourceInfo? ReadSourceInfo(JsonElement task, List<string> warnings)
    {
        if (!JsonProps.TryGet(task, "source_info", out var s) || s.ValueKind != JsonValueKind.Object) return null;

        string? Str(string name)
        {
            if (JsonProps.GetString(s, name, out var v) == ReadResult.WrongType) warnings.Add($"'source_info.{name}' sai kiểu (cần chuỗi), bỏ qua");
            return v;
        }

        List<double>? gps = null;
        if (JsonProps.TryGet(s, "gpscoord", out var g) && g.ValueKind == JsonValueKind.Array)
        {
            gps = [];
            foreach (var item in g.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var d))
                {
                    gps.Add(d);
                }
                else
                {
                    warnings.Add("'source_info.gpscoord' có phần tử không phải số, bỏ qua cả trường");
                    gps = null;
                    break;
                }
            }
        }

        return new VmsSourceInfo(Str("model"), Str("name"), gps, Str("location"), Str("address"));
    }

    private static List<VmsDetector>? ReadDetectors(JsonElement task, List<string> warnings)
    {
        if (!JsonProps.TryGet(task, "detector_conf", out var d) || d.ValueKind != JsonValueKind.Array) return null;

        var list = new List<VmsDetector>();
        foreach (var item in d.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                warnings.Add("'detector_conf' có phần tử không phải object, bỏ qua phần tử đó");
                continue;
            }

            JsonProps.GetString(item, "guid", out var guid);
            JsonProps.GetDouble(item, "rate", out var rate);
            list.Add(new VmsDetector(guid, rate));
        }

        return list;
    }

    private static TaskParseResult Rejected(string? taskId, string error) => new(null, taskId, error);
}
