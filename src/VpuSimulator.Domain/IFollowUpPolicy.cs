using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
    public interface IFollowUpPolicy
    {
        // Đúng 1 lần rút số khi rule có follow-up. Trả null = không follow-up.
        FollowUpKind? Decide(
            string ruleName,
            ActiveFollowUpState active, 
            Random rng);
    }
}
