using VpuSimulator.Domain;
using VpuSimulator.Domain.Entities;

namespace VpuSimulator.Application.Interfaces.Simulation;

// Cho Control API đọc trạng thái các unit đang chạy (phục vụ GET /api/v1/units và luồng SSE /api/v1/stream)
public interface IEmissionUnitQueryService
{
    UnitInfo? GetByTaskAndRoi(string appId, string taskId, int roiId);
    IReadOnlyList<UnitInfo> GetAll(string appId);
}
