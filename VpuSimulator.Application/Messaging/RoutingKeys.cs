namespace VpuSimulator.Application.Messaging;

//Routing key chỉ khai báo cái đang dùng; thêm dần khi cần
public static class RoutingKeys
{
    //Status và heartbeat của 1 identity
    public static string Status(string appId) => $"inbound.status.vpu.{appId}";
}
