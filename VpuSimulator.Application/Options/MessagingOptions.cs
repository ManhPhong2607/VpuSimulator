namespace VpuSimulator.Application.Options;

/// <summary>
/// Gốc cấu hình phần nhắn tin, bind từ mục Messaging trong appsettings.
/// Khoá dùng PascalCase theo quy ước .NET (AppId, VirtualHost...), tương ứng app_id, virtual_host...
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>
    /// Các target broker theo tên. Tên BrokerTargetOptions.DefaultName dùng cho identity không chỉ định Broker
    /// Không phân biệt hoa thường.
    /// </summary>
    public Dictionary<string, BrokerTargetOptions> Brokers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<IdentityOptions> Identities { get; set; } = [];
}
