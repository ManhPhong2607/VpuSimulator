using Serilog.Core;
using Serilog.Events;
using VpuSimulator.Application.Logging;
using VpuSimulator.Application.Messaging;
using VpuSimulator.Application.Scheduling;

namespace VpuSimulator.Api.Logging;

// Ống dẫn của Serilog: mỗi dòng log đi qua đây được chép vào LogBuffer để Console đọc qua GET /logs.
public sealed class LogBufferSink(LogBuffer buffer) : ILogEventSink
{
    public void Emit(LogEvent e)
    {
        string? identity = Text(e, "AppId");
        string? task = Text(e, "TaskId") ?? Text(e, "Task");
        string? rule = Text(e, "Rule") ?? Text(e, "RuleName");

        // Nhiều dòng log ghi "{Unit}" (UnitKey = app/task/roi/rule): tách ra để điền các field còn thiếu.
        if (e.Properties.TryGetValue("Unit", out var u) && u is ScalarValue sv)
        {
            if (sv.Value is UnitKey key)
            {
                identity ??= key.AppId;
                task ??= key.TaskId;
                rule ??= key.RuleName;
            }
            else if (sv.Value is string s)
            {
                // Chuỗi có dạng "appId/taskId/roiId/ruleName"
                var parts = s.Split('/', 4);
                if (parts.Length == 4)
                {
                    identity ??= parts[0];
                    task ??= parts[1];
                    rule ??= parts[3];
                }
            }
        }

        var outcome = OutcomeOf(e.Level);
        LogMessageInfo? message = null;
        string detail;

        // Dòng "message sent" (FR-OBS-3): tách routing key, loại, kích thước, payload ra khối message{...}
        if (e.MessageTemplate.Text.StartsWith("message sent", StringComparison.Ordinal)
            && Text(e, "RoutingKey") is { } routingKey
            && e.Properties.TryGetValue("SizeBytes", out var size) && size is ScalarValue { Value: int bytes })
        {
            var kind = Text(e, "Kind") ?? "other";
            var truncated = e.Properties.TryGetValue("Truncated", out var t) && t is ScalarValue { Value: true };
            message = new LogMessageInfo("message sent", routingKey, kind, bytes, Text(e, "Payload"), truncated);
            // Theo ví dụ 9.9: bản tin data là "emitted"; bản tin status là "status" (cả hai nằm trong kiểu LogOutcome)
            outcome = kind switch { "data" => "emitted", "status" => "status", _ => outcome };
            task ??= RoutingKeys.TaskIdOf(routingKey);
            // detail gọn, KHÔNG chép payload vào đây (payload đã nằm ở message.payload; chép hai lần tốn bộ nhớ)
            detail = $"{kind} {routingKey} ({bytes} bytes)";
        }
        else
        {
            detail = e.RenderMessage();
            // Chỉ ghi loại và nội dung lỗi, không ghi stack trace: dòng log hiện trên Console phải gọn.
            if (e.Exception is not null)
                detail += $" | {e.Exception.GetType().Name}: {e.Exception.Message}";
        }

        buffer.Add(e.Timestamp.ToUniversalTime(), outcome, identity, task, rule, detail, message);
    }

    private static string? Text(LogEvent e, string name) =>
        e.Properties.TryGetValue(name, out var v) && v is ScalarValue sv ? sv.Value?.ToString() : null;

    private static string OutcomeOf(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose or LogEventLevel.Debug => "debug",
        LogEventLevel.Information => "info",
        LogEventLevel.Warning => "warning",
        _ => "error",
    };
}