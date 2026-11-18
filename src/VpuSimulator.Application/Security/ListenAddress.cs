using System.Net;

namespace VpuSimulator.Application.Security;

public static class ListenAddress
{
    /// <summary>true khi mọi URL trong danh sách (ngăn cách bằng ';') chỉ lắng nghe loopback. Chuỗi rỗng coi là loopback (mặc định của Kestrel).</summary>
    public static bool IsLoopbackOnly(string? urls)
    {
        if (string.IsNullOrWhiteSpace(urls)) return true;

        foreach (var raw in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // "http://*:5000", "http://+:5000" không parse được thành Uri => coi là không an toàn
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return false;
            if (!IsLoopbackHost(uri.Host)) return false;
        }
        return true;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip);
    }
}