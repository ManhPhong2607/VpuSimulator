namespace VpuSimulator.Application.Messaging;

/// <summary>
/// Một bản tin VMS gửi xuống identity, giữ nguyên dạng thô: chưa parse JSON, chưa kiểm tra gì.
/// Việc parse và validate thuộc bước sau (SRS IF-CMD-1..6), không thuộc lớp broker.
/// </summary>
/// <param name="RoutingKey">Routing key thật của bản tin, ví dụ <c>outbound.vpu.vpu</c> hoặc <c>outbound.vpu.vpu.task.x</c>.</param>
/// <param name="Body">
/// Bản SAO riêng của body (không dùng chung bộ nhớ với thư viện RabbitMQ, vốn chỉ hợp lệ trong lúc
/// xử lý sự kiện). Bên nhận được giữ tuỳ ý, kể cả sau khi callback trả về.
/// </param>
public sealed record InboundMessage(string RoutingKey, byte[] Body);
