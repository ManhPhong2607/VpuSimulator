using System.Security.Cryptography;
using System.Text;

namespace VpuSimulator.Application.Security;

/// <summary>
/// Kiểm tra header Authorization: Bearer (SRS NFR-SEC). So sánh thời gian không đổi trên hash SHA-256
/// nên không lộ độ dài token. Token rỗng nghĩa là tắt xác thực.
/// </summary>
public sealed class BearerTokenValidator
{
    private const string Prefix = "Bearer ";
    private readonly byte[]? _expectedHash;

    public BearerTokenValidator(string? token)
    {
        _expectedHash = string.IsNullOrEmpty(token)
            ? null
            : SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    public bool Enabled => _expectedHash is not null;

    public bool IsValid(string? authorizationHeader)
    {
        if (_expectedHash is null) return true;
        if (authorizationHeader is null ||
            !authorizationHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var presented = authorizationHeader[Prefix.Length..];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        return CryptographicOperations.FixedTimeEquals(hash, _expectedHash);
    }
}