namespace VpuSimulator.Application.Options;

//Cấu hình 1 identity — identities[].app_id / heartbeat (+ broker là mở rộng)
public sealed class IdentityOptions
{
    //Bắt buộc, khác rỗng, duy nhất
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Chu kỳ heartbeat dạng duration (500ms, 30s, 5m, 6h)
    /// Để trống hoặc ≤ 0 nghĩa là 30 s
    /// </summary>
    public string? Heartbeat { get; set; }
    
    // Tên target broker mà identity này kết nối BrokerTargetOptions.DefaultName
    public string? Broker { get; set; }
}
