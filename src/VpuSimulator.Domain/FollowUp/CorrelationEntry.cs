using System;
using System.Collections.Generic;
using System.Text;

namespace VpuSimulator.Domain.FollowUp;

// 1 mục correlation để xem qua Control API (FR-COR-5): GET /correlation
public sealed record CorrelationEntry(string AppId, string Key, object Value, DateTimeOffset WrittenAt);