namespace VpuSimulator.Application.Identity;

public static class AppIdRules
{
    // VMS bind "inbound.status.vpu.*" (một từ) nên app_id không được có '.', '*', '#', khoảng trắng.
    public const int MaxLength = 64;

    public static bool IsValid(string? appId) =>
        !string.IsNullOrEmpty(appId) && appId.Length <= MaxLength &&
        appId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}