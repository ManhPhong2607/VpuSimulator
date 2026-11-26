using System.Text.Json;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using VpuSimulator.Api.Hosting;
using VpuSimulator.Application.Identity;
using VpuSimulator.Api.Endpoints;
using VpuSimulator.Api.Logging;
using VpuSimulator.Application.Logging;
using VpuSimulator.Application.Security;

var builder = WebApplication.CreateBuilder(args);

// Log: công tắc mức log (đổi lúc chạy, FR-OBS-2) + bộ đệm 2000 dòng cho Console
// Mức log ban đầu lấy từ cấu hình (Log:Level); không đặt thì là info. Giá trị sai thì dừng ngay, không bỏ qua lặng lẽ
var levelText = builder.Configuration["Log:Level"];
var initialLevel = LogEventLevel.Information;
if (!string.IsNullOrWhiteSpace(levelText) && !LogLevels.TryParse(levelText, out initialLevel))
    throw new InvalidOperationException(
        $"Log:Level = '{levelText}' không hợp lệ. Chấp nhận: debug, info, warn, error.");
var levelSwitch = new LoggingLevelSwitch(initialLevel);
var logBuffer = new LogBuffer(builder.Configuration.GetValue("Log:BufferSize", 2000));
var runtimeLog = new RuntimeLogSettings(
    verbose: builder.Configuration.GetValue("Log:Verbose", false),
    maxBodyLog: builder.Configuration.GetValue("Log:MaxBodyLog", 4096));
builder.Services.AddSingleton(levelSwitch);
builder.Services.AddSingleton(logBuffer);
builder.Services.AddSingleton(runtimeLog);

builder.Services.AddSingleton<VpuSimulator.Application.Events.SseEventBus>();

builder.Host.UseSerilog((_, _, logging) => logging
    .MinimumLevel.ControlledBy(levelSwitch)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning) // giữ log request ồn ào ngoài bộ đệm
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Sink(new LogBufferSink(logBuffer)));

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

app.UseMiddleware<BearerAuthMiddleware>();

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
app.MapGet("/api/v1/stream",
    async (HttpContext ctx, VpuSimulator.Application.Events.SseEventBus bus, CancellationToken ct) =>
    {
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        using var sub = bus.Subscribe(out var reader);
        await ctx.Response.Body.FlushAsync(ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    var ev = await reader.ReadAsync(timeoutCts.Token);
                    await ctx.Response.WriteAsync($"event: {ev.Type}\ndata: {JsonSerializer.Serialize(ev.Data)}\n\n",
                        ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Quá 5s không có sự kiện -> gửi heartbeat giữ kết nối
                    var hb = new { type = "heartbeat", timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                    await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(hb)}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            /* client ngắt kết nối */
        }
    });

// Control API: các endpoint đọc
app.MapControlApiEndpoints();

// Fallback SPA route cho Next.js Console client-side routing
app.MapFallbackToFile("index.html");

app.Run();