using System.Text.Json;
using VpuSimulator.Application.Commands;
using VpuSimulator.Application.Commands.Parsing;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Options;

namespace VpuSimulator.Application.Scenario;

public static class ScenarioSeverity
{
    public const string Error = "error";
    public const string Warning = "warning";
}

public sealed record ScenarioIssue(string Severity, string Title, string Detail);

public sealed record PlannedIdentity(IdentityOptions Options, IReadOnlyList<ParsedTask> Tasks);

public sealed record ScenarioPlan(
    IReadOnlyList<ScenarioIssue> Issues,
    IReadOnlyList<PlannedIdentity> Identities,
    int OkIdentities,
    int OkTasks,
    long? Seed)
{
    public bool HasErrors => Issues.Any(i => i.Severity == ScenarioSeverity.Error);
}

/// <summary>
/// Validate TOÀN BỘ scenario trước khi áp dụng. Hàm thuần: không đụng broker/kho task
/// Rule chưa hỗ trợ là cảnh báo, mọi lỗi khác chặn cả scenario
/// </summary>
public static class ScenarioPlanner
{
    public static ScenarioPlan Plan(
        JsonElement doc,
        long runningSeed,
        IReadOnlyCollection<string> existingAppIds,
        Func<string, bool> profileExists,
        Func<string, int?, bool>? isSupportedRule = null)
    {
        var issues = new List<ScenarioIssue>();
        var planned = new List<PlannedIdentity>();
        var okIdentities = 0;
        var okTasks = 0;
        long? seed = null;

        void Error(string title, string detail) => issues.Add(new(ScenarioSeverity.Error, title, detail));
        void Warn(string title, string detail) => issues.Add(new(ScenarioSeverity.Warning, title, detail));
        int ErrorCount() => issues.Count(i => i.Severity == ScenarioSeverity.Error);
        ScenarioPlan Done() => new(issues, planned, okIdentities, okTasks, seed);

        if (doc.ValueKind != JsonValueKind.Object)
        {
            Error("Scenario không phải object JSON", "Gốc của scenario phải là object có trường identities.");
            return Done();
        }

        if (TryProp(doc, "seed", out var seedEl) && seedEl.ValueKind != JsonValueKind.Null)
        {
            if (seedEl.ValueKind != JsonValueKind.Number || !seedEl.TryGetInt64(out var s))
            {
                Error("seed sai kiểu", "seed phải là số nguyên.");
            }
            else if (s != runningSeed)
            {
                Error($"seed không khớp: {s}",
                    $"Hệ thống đang chạy với seed {runningSeed}; seed chỉ đặt lúc khởi động nên scenario phải trùng hoặc bỏ trường này.");
            }
            else
            {
                seed = s;
            }
        }

        if (!TryProp(doc, "identities", out var ids) || ids.ValueKind != JsonValueKind.Array || ids.GetArrayLength() == 0)
        {
            Error("Danh sách identities rỗng", "Scenario phải có ít nhất một identity trong mảng identities.");
            return Done();
        }

        var seenApp = new HashSet<string>(StringComparer.Ordinal);
        var existing = new HashSet<string>(existingAppIds, StringComparer.Ordinal);
        var index = -1;

        foreach (var item in ids.EnumerateArray())
        {
            index++;
            var at = $"identities[{index}]";
            var errorsBefore = ErrorCount();

            if (item.ValueKind != JsonValueKind.Object)
            {
                Error($"Phần tử không phải object ({at})", "Mỗi identity phải là object.");
                continue;
            }

            // app_id
            string? appId = null;
            if (!TryProp(item, "app_id", out var a) || a.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(appId = a.GetString()))
            {
                Error($"app_id thiếu hoặc rỗng ({at})", "app_id bắt buộc, kiểu chuỗi, khác rỗng.");
            }
            else if (!AppIdRules.IsValid(appId))
            {
                Error($"app_id không hợp lệ: {appId}",
                    $"{at}: tối đa {AppIdRules.MaxLength} ký tự, chỉ gồm chữ, số, '-' và '_'.");
            }
            else if (!seenApp.Add(appId))
            {
                Error($"app_id trùng: {appId}", $"{at}: xuất hiện nhiều lần trong scenario.");
            }
            else if (existing.Contains(appId))
            {
                Error($"app_id trùng: {appId}", $"{at}: đã tồn tại trong hệ thống.");
            }

            // profile
            string? profile = null;
            if (TryProp(item, "profile", out var p) && p.ValueKind != JsonValueKind.Null)
            {
                if (p.ValueKind != JsonValueKind.String)
                {
                    Error($"profile sai kiểu ({at})", "profile phải là chuỗi.");
                }
                else if (!string.IsNullOrWhiteSpace(profile = p.GetString()) && !profileExists(profile!))
                {
                    Error($"profile không tồn tại: {profile}", $"{at}: không có profile này trong hệ thống.");
                }
            }

            // heartbeat
            string? heartbeat = null;
            if (TryProp(item, "heartbeat", out var h) && h.ValueKind != JsonValueKind.Null)
            {
                if (h.ValueKind != JsonValueKind.String)
                {
                    Error($"heartbeat sai kiểu ({at})", "heartbeat phải là chuỗi duration, ví dụ 30s.");
                }
                else if (!string.IsNullOrWhiteSpace(heartbeat = h.GetString())
                         && !DurationParser.TryParse(heartbeat, out _))
                {
                    Error($"heartbeat không hợp lệ: {heartbeat}", $"{at}: ví dụ 500ms, 30s, 5m, 6h.");
                }
            }

            // tasks
            var tasks = new List<ParsedTask>();
            if (TryProp(item, "tasks", out var tl) && tl.ValueKind != JsonValueKind.Null)
            {
                if (tl.ValueKind != JsonValueKind.Array)
                {
                    Error($"tasks sai kiểu ({at})", "tasks phải là mảng.");
                }
                else
                {
                    var seenTask = new HashSet<string>(StringComparer.Ordinal);
                    var j = -1;
                    foreach (var te in tl.EnumerateArray())
                    {
                        j++;
                        var tAt = $"{at}.tasks[{j}]";
                        var res = VmsTaskParser.ParseTask(te, isSupportedRule);
                        if (res.IsRejected)
                        {
                            Error($"Task bị từ chối ({tAt})", res.Error ?? "task không hợp lệ.");
                            continue;
                        }

                        var task = res.Value!;
                        if (!seenTask.Add(task.TaskId))
                        {
                            Error($"task id trùng: {task.TaskId}", $"{tAt}: trùng trong cùng identity.");
                            continue;
                        }

                        foreach (var w in task.Warnings)
                        {
                            Warn($"Cảnh báo task ({tAt})", w);
                        }
                        tasks.Add(task);
                    }
                }
            }

            if (ErrorCount() == errorsBefore)
            {
                okIdentities++;
                okTasks += tasks.Count;
                planned.Add(new PlannedIdentity(
                    new IdentityOptions { AppId = appId!, Heartbeat = heartbeat, Profile = profile },
                    tasks));
            }
        }

        return Done();
    }

    private static bool TryProp(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}