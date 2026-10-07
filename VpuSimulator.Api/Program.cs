using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Parse("127.0.0.1"), 18080);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Phục vụ Single-File Static Console nhúng sẵn theo ràng buộc CON-5
app.UseDefaultFiles();
app.UseStaticFiles();

// Health check routes
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", uptime = Environment.TickCount64 / 1000, version = "1.0.0" }));
app.MapGet("/readyz", () => Results.Ok(new { status = "ready" }));

// Server-Sent Events endpoint chuẩn hóa theo contract v3
app.MapGet("/api/v1/stream", async (HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("Connection", "keep-alive");

    while (!ct.IsCancellationRequested)
    {
        var heartbeat = new { type = "heartbeat", timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(heartbeat)}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
        await Task.Delay(5000, ct);
    }
});

// Fallback SPA route cho Next.js Console client-side routing
app.MapFallbackToFile("index.html");

app.Run();