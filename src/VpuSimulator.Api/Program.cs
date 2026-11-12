using System.Text.Json;
using VpuSimulator.Api.Hosting;
using VpuSimulator.Application.Identity;
using VpuSimulator.Api.Endpoints;
using VpuSimulator.Application.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddVpuMessaging(builder.Configuration);

var listen = builder.Configuration["Api:Listen"] ?? "http://127.0.0.1:18080";
var tokenValidator = new BearerTokenValidator(builder.Configuration["Api:Token"]);

// NFR-SEC: không mở listener ra ngoài loopback khi chưa có token
if (!tokenValidator.Enabled && !ListenAddress.IsLoopbackOnly(listen))
    throw new InvalidOperationException(
        $"Api:Listen='{listen}' không phải loopback nhưng chưa cấu hình Api:Token. " +
        "Đặt biến môi trường Api__Token hoặc chỉ lắng nghe 127.0.0.1.");

builder.Services.AddSingleton(tokenValidator);
builder.WebHost.UseUrls(listen);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Phục vụ Single-File Static Console nhúng sẵn
app.UseDefaultFiles();
app.UseStaticFiles();

// Health check routes
app.MapGet("/healthz", (AppClock clock, TimeProvider time) =>
    Results.Ok(new { status = "healthy", uptime = clock.UptimeSeconds(time), version = "1.0.0" }));

app.MapGet("/readyz", (IdentityLifecycleService identities) =>
{
    var statuses = identities.GetStatuses();
    var report = ReadinessEvaluator.Evaluate(statuses);
    var body = new
    {
        status = report.IsReady ? "ready" : "not_ready",
        reasons = report.Reasons,
        identities = report.Identities,
        connected = report.Connected,
        details = statuses,
    };
    return report.IsReady ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
});

// Server-Sent Events endpoint chuẩn hóa theo contract
app.MapGet("/api/v1/stream", async (HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    try
    {
        while (!ct.IsCancellationRequested)
        {
            var hb = new { type = "heartbeat", timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(hb)}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
            await Task.Delay(5000, ct);
        }
    }
    catch (OperationCanceledException) { /* client ngắt kết nối */ }
});
// Control API: các endpoint đọc
app.MapControlApiEndpoints();

// Fallback SPA route cho Next.js Console client-side routing
app.MapFallbackToFile("index.html");

app.Run();