using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain
{
    public interface ICorrelationStore
    {
        void Write(string appId, string key, object value);
        object? Read(string appId, string key);
        IReadOnlyDictionary<string, object> GetAll(string appId);
        bool TryDelete(string appId, string key);   // nguyên tử: true = lần gọi này chiếm được
        void Clear(string appId);    // gỡ identity thì xoá correlation của nó
        void ClearAll();
    }

    public sealed record LicensePlateCorrelationValue(string EventKey, string Label, string? Plate, DateTimeOffset WrittenAt);
}
