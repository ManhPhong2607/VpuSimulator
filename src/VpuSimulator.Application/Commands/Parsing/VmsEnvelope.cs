using System.Text.Json;

namespace VpuSimulator.Application.Commands;

/// <summary>
/// Vỏ ngoài của một lệnh VMS Uuid, Scope, Timestamp
/// chỉ để ghi log: không bao giờ dùng để từ chối lệnh, nên đọc lỏng và cho phép null.
/// </summary>
/// Detai Bản sao riêng, dùng được sau khi parse xong. ValueKind == Undefined nếu bản tin không có detail
public sealed record VmsEnvelope(string? Uuid, string? Scope, long? Timestamp, string Command, JsonElement Detail);

public enum VmsParseFailureReason
{
    // Không phải JSON hợp lệ (IF-CMD-3)
    InvalidJson,

    //JSON hợp lệ nhưng gốc không phải object
    NotAnObject,

    //Thiếu command, sai kiểu, hoặc rỗng
    MissingCommand,

    //command không thuộc 5 lệnh được hỗ trợ
    UnknownCommand,
}

//Preview Tối đa 512 byte đầu của bản tin, để ghi log
public sealed record VmsParseFailure(VmsParseFailureReason Reason, string Preview);

public sealed record VmsEnvelopeParseResult(VmsEnvelope? Envelope, VmsParseFailure? Failure)
{
    public bool IsSuccess => Envelope is not null;
}
