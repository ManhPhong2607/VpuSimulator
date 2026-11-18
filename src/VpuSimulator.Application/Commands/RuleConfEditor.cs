using System.Text.Json;
using VpuSimulator.Application.Commands.Parsing;
using VpuSimulator.Application.State;

namespace VpuSimulator.Application.Commands;

public enum RuleConfReplaceOutcome
{
    Replaced,
    TaskNotFound,
    RuleNotFound,
    Rejected, // body sai (id/name không khớp, sai kiểu) hoặc bản trộn bị từ chối: task cũ được giữ nguyên
}

public sealed record RuleConfReplaceResult(RuleConfReplaceOutcome Outcome, string? Error = null);

/// <summary>
/// Thay rule_conf của MỘT rule trong task đang chạy (Control API: PUT .../rule-conf)
/// Đi qua cùng đường trộn với update-task (TaskMerge) unit giữ nguyên trạng thái chạy
/// và rule khác (kể cả rule bị bỏ qua) không bị đụng tới. Key lạ trong body được giữ nguyên
/// </summary>
public static class RuleConfEditor
{
    public static RuleConfReplaceResult Replace(
        ITaskStore store, string appId, string taskId, int roiId, string ruleName, JsonElement newRule,
        Func<string, int?, bool>? isSupportedRule = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(ruleName);

        if (newRule.ValueKind != JsonValueKind.Object)
        {
            return Rejected("Body phải là một object rule_conf.");
        }

        if (JsonProps.GetInt(newRule, "id", out var bodyId) != ReadResult.Ok || bodyId != roiId)
        {
            return Rejected($"'id' trong body phải là số nguyên và bằng {roiId} (khớp đường dẫn).");
        }

        if (JsonProps.GetString(newRule, "name", out var bodyName) != ReadResult.Ok
            || !string.Equals(bodyName, ruleName, StringComparison.Ordinal))
        {
            return Rejected($"'name' trong body phải bằng '{ruleName}' (khớp unit trong đường dẫn).");
        }

        var ruleFound = false;
        string? rejection = null;

        var result = store.Update(appId, taskId, existing =>
        {
            if (!JsonProps.TryGet(existing.Raw, "rule_conf", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return existing;
            }

            // Dựng bản vá {"rule_conf":[...]} từ rule_conf GỐC của task: chỉ phần tử (id, name) đích bị thay,
            // các phần tử khác (kể cả rule chưa hỗ trợ) giữ nguyên từng byte
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("rule_conf");
                writer.WriteStartArray();
                foreach (var element in array.EnumerateArray())
                {
                    if (!ruleFound && IsTarget(element, roiId, ruleName))
                    {
                        newRule.WriteTo(writer);
                        ruleFound = true;
                    }
                    else
                    {
                        element.WriteTo(writer);
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            if (!ruleFound)
            {
                return existing;
            }

            using var patch = JsonDocument.Parse(stream.ToArray());
            var merged = TaskMerge.Apply(existing, patch.RootElement, isSupportedRule);
            if (merged.IsRejected)
            {
                rejection = merged.Error ?? "rule_conf bị từ chối.";
                return existing;
            }

            // Rule mới phải còn là một rule hợp lệ với đúng khoá cũ, nếu không unit sẽ biến mất
            if (!merged.Value!.Rules.Any(r =>
                    r.RoiId == roiId && string.Equals(r.Name, ruleName, StringComparison.Ordinal)))
            {
                rejection = merged.Value.Warnings.Count > 0
                    ? string.Join("; ", merged.Value.Warnings)
                    : "rule_conf mới không còn là một rule hợp lệ cho unit này.";
                return existing;
            }

            return merged.Value;
        });

        if (result is null)
        {
            return new RuleConfReplaceResult(RuleConfReplaceOutcome.TaskNotFound);
        }

        if (!ruleFound)
        {
            return new RuleConfReplaceResult(RuleConfReplaceOutcome.RuleNotFound);
        }

        return rejection is null
            ? new RuleConfReplaceResult(RuleConfReplaceOutcome.Replaced)
            : Rejected(rejection);
    }

    private static bool IsTarget(JsonElement element, int roiId, string ruleName) =>
        JsonProps.GetInt(element, "id", out var id) == ReadResult.Ok && id == roiId
                                                                     && JsonProps.GetString(element, "name",
                                                                         out var name) == ReadResult.Ok
                                                                     && string.Equals(name, ruleName,
                                                                         StringComparison.Ordinal);

    private static RuleConfReplaceResult Rejected(string error) =>
        new(RuleConfReplaceOutcome.Rejected, error);
}