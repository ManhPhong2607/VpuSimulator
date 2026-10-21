namespace VpuSimulator.Application.Interfaces;

/// <summary>
/// Tạo session broker theo tên target. "Target" = 1 bộ cấu hình kết nối (host, vhost, user, exchange...).
/// Nhờ vậy cùng 1 đoạn code heartbeat có thể bắn tới nhiều đích khác nhau (exchange khác, vhost khác,
/// broker khác) chỉ bằng cách đổi tên target trong cấu hình identity — code heartbeat không biết exchange nào
/// </summary>
public interface IMessageBrokerFactory
{
    //Tên target có được cấu hình không. Dùng để validate trước khi tạo identity/nạp scenario
    bool Exists(string brokerName);

    //Tạo 1 session MỚI, riêng cho appId, trên target brokerName
    //Khi brokerName không tồn tại sẽ gọi Exists
    IBrokerSession CreateSession(string brokerName, string appId);
}
