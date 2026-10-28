using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
    // Phần thay đổi mỗi lần phát, do emitter của bạn A cấp (không phải cấu hình của rule)
    public sealed record TickContext(
        Random Rng,                     // Random riêng của unit
        DateTimeOffset Now,             // MỘT thời điểm cho cả lần phát
        ICorrelationStore Correlation,
        FixedValues FixedValues,
        SimulationMode Mode = SimulationMode.Simulated);
}

