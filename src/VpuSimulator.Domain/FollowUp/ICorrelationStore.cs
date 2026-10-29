using System.Collections.Generic;

namespace VpuSimulator.Domain;

// Lưu trữ liên kết giữa các bài toán (#1 ghi biển số, #6 đọc biển số cho xe vượt đèn đỏ), scope theo appId
public interface ICorrelationStore
{
    void Write(string appId, string key, object value);
    object? Read(string appId, string key);
    IReadOnlyDictionary<string, object> GetAll(string appId);
    void Delete(string appId, string key);
}
