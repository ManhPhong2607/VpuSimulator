namespace VpuSimulator.Application.Identity;

/// <summary>Kết quả đánh giá sẵn sàng. Reasons rỗng khi sẵn sàng.</summary>
public sealed record ReadinessReport(bool IsReady, IReadOnlyList<string> Reasons, int Identities, int Connected);

/// <summary>
/// Quy tắc /readyz: 503 khi không có identity nào hoặc có identity chưa kết nối
/// Dependency khác sẽ được thêm vào đây khi có
/// Không đưa thông điệp lỗi hay thông tin kết nối vào kết quả 
/// </summary>
public static class ReadinessEvaluator
{
    public const string NoIdentity = "no_identity";
    public const string IdentityDown = "identity_down";

    public static ReadinessReport Evaluate(IReadOnlyList<IdentityStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        var connected = statuses.Count(s => s.IsConnected);
        var reasons = new List<string>();
        if (statuses.Count == 0)
        {
            reasons.Add(NoIdentity);
        }
        else if (connected < statuses.Count)
        {
            reasons.Add(IdentityDown);
        }

        return new ReadinessReport(reasons.Count == 0, reasons, statuses.Count, connected);
    }
}