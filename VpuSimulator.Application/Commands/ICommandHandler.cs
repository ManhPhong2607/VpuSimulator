using VpuSimulator.Application.Interfaces;
using VpuSimulator.Application.Messaging;

namespace VpuSimulator.Application.Commands;

/// <summary>Mọi thứ một handler cần để xử lý một lệnh của một identity.</summary>
/// <param name="AppId">Identity nhận lệnh, theo routing key (IF-CMD-6), không phải <c>detail.appId</c>.</param>
/// <param name="Broker">Session của identity đó, để phát status.</param>
public sealed record CommandContext(string AppId, IMessageBroker Broker, InboundMessage Message, VmsEnvelope Envelope);

/// <summary>Xử lý đúng một loại lệnh VMS. Mỗi handler đăng ký một giá trị <see cref="Command"/> (xem <see cref="VmsCommandNames"/>).</summary>
public interface ICommandHandler
{
    string Command { get; }

    /// <summary>
    /// Dữ liệu xấu thì ghi log, đếm rồi trở về bình thường; không ném exception.
    /// Chỉ ném khi bị huỷ hoặc gặp lỗi lập trình, và dispatcher sẽ bắt nốt.
    /// </summary>
    Task HandleAsync(CommandContext context, CancellationToken cancellationToken);
}
