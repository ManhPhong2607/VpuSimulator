using VpuSimulator.Application.Security;

namespace VpuSimulator.Api.Hosting;

/// <summary>Chặn /api/** và /metrics khi thiếu hoặc sai Bearer token. /healthz, /readyz và file tĩnh của console được miễn.</summary>
public sealed class BearerAuthMiddleware(RequestDelegate next, BearerTokenValidator validator)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (!validator.Enabled || !RequiresAuth(context.Request.Path) ||
            validator.IsValid(context.Request.Headers.Authorization.ToString()))
            return next(context);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
    }

    private static bool RequiresAuth(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/metrics");
}