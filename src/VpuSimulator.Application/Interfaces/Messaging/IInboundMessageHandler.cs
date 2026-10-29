using VpuSimulator.Application.Interfaces.Messaging;
using VpuSimulator.Application.Messaging;

namespace VpuSimulator.Application.Interfaces;

/// <summary>
/// Nơi nhận mọi bản tin VMS gửi xuống các identity. Cài đặt thật là VmsCommandDispatcher (parse envelope, chuyển cho ICommandHandler).
/// Đăng ký là tuỳ chọn: không có thì lệnh chỉ được ghi log Debug.
/// </summary>
public interface IInboundMessageHandler
{
    /// <summary>
    /// Được gọi cho từng bản tin của identity appId, TUẦN TỰ (xong bản tin này mới tới bản tin sau).
    /// Chạy ngay trên luồng nhận của broker, nên phải trả về nhanh: việc nặng hãy đẩy sang hàng đợi nền.
    /// Exception ném ra sẽ bị session nuốt và ghi log, bản tin KHÔNG được gửi lại.
    /// </summary>
    /// <param name="broker">Session của chính identity đó, để handler phát status (không có đường nào khác để biết session).</param>
    Task HandleAsync(string appId, IMessageBroker broker, InboundMessage message, CancellationToken cancellationToken);
}
