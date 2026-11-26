using System.Text;
using VpuSimulator.Application.Messaging;

namespace VpuSimulator.Application.Logging;

// Những gì cần ghi cho một bản tin đã gửi: loại, kích thước, và payload (chỉ khi verbose).
public readonly record struct SentMessageInfo(string Kind, int SizeBytes, string? Payload, bool Truncated);

// Logic thuần (không dính broker hay logger) nên test được mà không cần RabbitMQ.
public static class MessageSentLog
{
    // includePayload=false (verbose tắt): payload = null. Dài hơn maxBodyLog ký tự thì cắt và đánh dấu truncated.
    public static SentMessageInfo Describe(string routingKey, byte[] body, bool includePayload, int maxBodyLog)
    {
        var kind = RoutingKeys.KindOf(routingKey);
        if (!includePayload)
            return new SentMessageInfo(kind, body.Length, null, false);

        var text = Encoding.UTF8.GetString(body);
        return text.Length <= maxBodyLog
            ? new SentMessageInfo(kind, body.Length, text, false)
            : new SentMessageInfo(kind, body.Length, text[..maxBodyLog], true);
    }
}