using VpuSimulator.Application.Identity;

namespace VpuSimulator.Application.Options;

/// <summary>
/// Kiểm tra cấu hình lúc khởi động và gom MỌI lỗi một lần (fail-fast, thông báo rõ), thay vì sửa từng lỗi một
/// Chỉ kiểm tra lỗi cấu hình. Broker đang tắt không phải lỗi cấu hình, nó được xử lý bằng reconnect
/// </summary>
public static class MessagingConfigValidator
{
    public static IReadOnlyList<string> Validate(MessagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();

        if (options.Brokers.Count == 0)
        {
            errors.Add("Messaging:Brokers phải có ít nhất 1 target (thường tên 'default').");
        }

        foreach (var (name, broker) in options.Brokers)
        {
            var path = $"Messaging:Brokers:{name}";
            if (string.IsNullOrWhiteSpace(broker.Host)) errors.Add($"{path}:Host không được rỗng.");
            if (broker.Port is < 1 or > 65535) errors.Add($"{path}:Port phải trong khoảng 1..65535 (đang là {broker.Port}).");
            if (string.IsNullOrWhiteSpace(broker.Exchange)) errors.Add($"{path}:Exchange không được rỗng.");
            if (broker.Prefetch < 0) errors.Add($"{path}:Prefetch không được âm.");
            if (broker.ReconnectMin <= TimeSpan.Zero) errors.Add($"{path}:ReconnectMin phải > 0.");
            if (broker.ReconnectMax < broker.ReconnectMin) errors.Add($"{path}:ReconnectMax phải >= ReconnectMin.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < options.Identities.Count; i++)
        {
            var identity = options.Identities[i];
            var path = $"Messaging:Identities[{i}]";

            if (string.IsNullOrWhiteSpace(identity.AppId))
            {
                errors.Add($"{path}:AppId không được rỗng.");
            }
            else if (!seen.Add(identity.AppId))
            {
                errors.Add($"{path}:AppId '{identity.AppId}' bị trùng.");
            }

            if (!string.IsNullOrWhiteSpace(identity.Heartbeat) && !DurationParser.TryParse(identity.Heartbeat, out _))
            {
                errors.Add($"{path}:Heartbeat '{identity.Heartbeat}' không đúng cú pháp (ví dụ 500ms, 30s, 5m, 6h).");
            }

            var brokerName = string.IsNullOrWhiteSpace(identity.Broker) ? BrokerTargetOptions.DefaultName : identity.Broker;
            if (!options.Brokers.ContainsKey(brokerName))
            {
                errors.Add($"{path}:Broker '{brokerName}' không có trong Messaging:Brokers.");
            }
        }

        return errors;
    }
}
