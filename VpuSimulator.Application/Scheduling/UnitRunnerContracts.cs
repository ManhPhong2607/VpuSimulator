using VpuSimulator.Domain;

namespace VpuSimulator.Application.Scheduling;

/// <summary>Thông tin scheduler đưa cho hàm phát một lần. PayloadRandom chỉ dùng trong lần phát này (mỗi unit chạy tuần tự).</summary>
public sealed record UnitEmitContext(UnitKey Key, int Epoch, long RunToken, Random PayloadRandom, bool IsManual, DateTimeOffset Now);

/// <summary>
/// Hàm "phát một lần". Phải tôn trọng cancellationToken: gỡ unit khi đang phát thì token bị huỷ (FR-SIM-15).
/// Kết quả: Emitted / NoMessage / Precondition / Error (FR-GEN-8). Ném exception được tính là Error.
/// </summary>
public delegate Task<RuleOutcome> UnitEmitDelegate(UnitEmitContext context, CancellationToken cancellationToken);

public enum TriggerResult
{
    Accepted,
    RejectedBusy,
    RejectedStopped,
}

/// <summary>Ảnh chụp trạng thái unit, phục vụ GET /api/v1/units (auto, lần phát cuối, lần phát kế, đang phát).</summary>
public sealed record UnitSnapshot(
    UnitKey Key,
    bool Auto,
    bool IsEmitting,
    int Epoch,
    DateTimeOffset? NextDueAt,
    DateTimeOffset? LastEmitAt,
    long Emitted,
    long NoMessage,
    long Precondition,
    long Errors,
    long TicksDropped,
    long TicksDeferredByMinGap,
    long TriggersRejected);