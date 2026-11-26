namespace VpuSimulator.Application.Messaging;

//Routing key chỉ khai báo cái đang dùng; thêm dần khi cần
public static class RoutingKeys
{
    //Status và heartbeat của 1 identity
    public static string Status(string appId) => $"inbound.status.vpu.{appId}";
    
    //Dữ liệu sự kiện AI của 1 task (theo SRS: inbound.data.vpu.<task_id>)
    public static string Data(string taskId) => $"inbound.data.vpu.{taskId}";
    
    private const string DataPrefix = "inbound.data.vpu.";
    private const string StatusPrefix = "inbound.status.vpu.";

    // Loại bản tin theo routing key, dùng cho log "message sent": data | status | other
    public static string KindOf(string routingKey) =>
        routingKey.StartsWith(DataPrefix, StringComparison.Ordinal) ? "data"
        : routingKey.StartsWith(StatusPrefix, StringComparison.Ordinal) ? "status"
        : "other";

    // task_id của bản tin data (phần sau tiền tố); không phải data thì null
    public static string? TaskIdOf(string routingKey) =>
        routingKey.StartsWith(DataPrefix, StringComparison.Ordinal) ? routingKey[DataPrefix.Length..] : null;
}
