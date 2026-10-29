using VpuSimulator.Domain.Entities;

namespace VpuSimulator.Domain.FollowUp;

public interface IFollowUpPolicy
{
    // Đúng 1 lần rút số khi rule có follow-up. Trả null = không follow-up.
    FollowUpKind? Decide(
        string ruleName,
        ActiveFollowUpState active, 
        Random rng);
}

public enum FollowUpKind { Deactivate, Discard }