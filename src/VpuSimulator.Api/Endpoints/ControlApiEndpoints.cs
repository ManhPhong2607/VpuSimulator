namespace VpuSimulator.Api.Endpoints;

// Đăng ký toàn bộ route Control API (SRS 3.1.10). Handler được tách thành các file partial theo nhóm chức năng.
public static partial class ControlApiEndpoints
{
    public static IEndpointRouteBuilder MapControlApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1").WithTags("Control API");

        // Hệ thống
        api.MapGet("/status", GetStatus);
        api.MapGet("/config", GetConfig);
        api.MapGet("/rules", GetRules);

        // VPU (Identity)
        api.MapGet("/identities", GetIdentities);
        api.MapPost("/identities", AddIdentity);
        api.MapDelete("/identities/{appId}", RemoveIdentity);
        api.MapPatch("/identities/{appId}", PatchIdentity);

        // VPS (Task)
        api.MapGet("/tasks", GetTasks);
        api.MapGet("/tasks/{appId}/{task}", GetTask);
        api.MapDelete("/tasks", StopAllTasks);
        api.MapPatch("/tasks/{appId}/{task}", PatchTask);
        api.MapDelete("/tasks/{appId}/{task}", RemoveTask);

        // Bài toán (Unit)
        api.MapGet("/units", GetUnits);
        api.MapGet("/units/{appId}/{task}/{roi}", GetUnit);
        api.MapPost("/units/{appId}/{task}/{roi}/trigger", TriggerUnit);
        api.MapPost("/units/{appId}/{task}/{roi}/auto", SetUnitAuto);
        api.MapPut("/units/{appId}/{task}/{roi}/rule-conf", PutRuleConf);

        // Profile
        api.MapGet("/profiles", GetProfiles);
        api.MapPost("/profiles/apply", ApplyProfile);
        api.MapPost("/profiles", CreateProfile);
        api.MapPut("/profiles/{name}", UpdateProfile);
        api.MapDelete("/profiles/{name}", DeleteProfile);
        api.MapPost("/profiles/{name}/reset", ResetProfile);

        // Scenario
        api.MapGet("/scenario", GetScenario);
        api.MapPost("/scenario", PostScenario);
        api.MapDelete("/scenario", DeleteScenario);

        // Correlation
        api.MapGet("/correlation", GetCorrelation);
        api.MapDelete("/correlation", ClearCorrelation);
        api.MapDelete("/correlation/{appId}/{key}", DeleteCorrelationEntry);

        // Log
        api.MapGet("/logs", GetLogs);
        api.MapPost("/logs/level", SetLogLevel);
        api.MapPost("/logs/verbose", SetLogVerbose);

        return app;
    }
}
