namespace VpuSimulator.Application.Interfaces;

/// <summary>
/// 1 identity = 1 session broker riêng. Session kế thừa IMessageBroker để publish,
/// và thêm trạng thái kết nối để heartbeat biết khi nào được phép gửi.
/// Infrastructure chịu trách nhiệm: mở kết nối tới đúng target (host/vhost/exchange),
/// subscribe outbound.vpu.*, và tự reconnect với backoff luỹ thừa có giới hạn
/// Heartbeat KHÔNG biết gì về các chi tiết đó.
/// </summary>
public interface IBrokerSession : IMessageBroker, IAsyncDisposable
{
    //True khi session đang kết nối và subscribe xong
    bool IsConnected { get; }

    /// <summary>
    /// Callback do bên dùng đặt trước StartAsync. Session phải gọi nó mỗi lần (re)subscribe
    /// thành công, kể cả sau reconnect — chính là thời điểm phát status running cấp process
    /// Exception ném ra từ callback phải được session nuốt và ghi log, không được làm sập vòng reconnect.
    /// </summary>
    Func<CancellationToken, Task>? OnConnectedAsync { get; set; }

    /// <summary>
    /// Bắt đầu kết nối ở nền rồi trả về ngay. KHÔNG đợi kết nối thành công và KHÔNG ném exception
    /// vì broker chưa sẵn sàng — lỗi kết nối được xử lý bằng reconnect
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);
}
