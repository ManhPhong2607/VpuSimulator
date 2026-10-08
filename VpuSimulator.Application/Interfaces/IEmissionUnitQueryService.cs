using System.Collections.Generic;
using VpuSimulator.Domain;

namespace VpuSimulator.Application.Interfaces;

// Cho Control API đọc trạng thái các unit đang chạy (phục vụ GET /api/v1/units và luồng SSE /api/v1/stream)
public interface IEmissionUnitQueryService
{
    EmissionUnit? GetByTaskAndRoi(string appId, string taskId, int roiId);
    IReadOnlyList<EmissionUnit> GetAll(string appId);
}
