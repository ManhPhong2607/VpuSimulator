namespace VpuSimulator.Domain;

// Hàm tra cứu 1 chiều: AiFlowCode -> AiFlowTypePayload (Config Group)
public interface IAiFlowGroupResolver
{
    AiFlowTypePayload Resolve(AiFlowCode aiFlowCode);
}
