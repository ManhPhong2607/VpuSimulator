// Lưu trữ liên kết giữa các bài toán (#1 ghi biển số, #6 đọc biển số cho xe vượt đèn đỏ), scope theo appId
namespace VpuSimulator.Domain.FollowUp
{
    public interface ICorrelationStore
    {
        void Write(string appId, string key, object value);
        object? Read(string appId, string key);
        IReadOnlyDictionary<string, object> GetAll(string appId);
        bool TryDelete(string appId, string key); // nguyên tử: true = lần gọi này chiếm được
        void Clear(string appId); // gỡ identity thì xoá correlation của nó
        void ClearAll();
    }

    public sealed record LicensePlateCorrelationValue(
        string EventKey,
        string Label,
        string? Plate,
        DateTimeOffset WrittenAt);
}