using System;
using System.Collections.Generic;
using System.Text;
using VpuSimulator.Domain;

namespace VpuSimulator.Application
{
    public interface IRuleHandlerRegistry
    {
        IRuleHandler? Resolve(string name, AiFlowCode aiFlowCode);
        IReadOnlyCollection<string> SupportedNames { get; }
    }
}