using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VpuSimulator.Application.Identity;
using VpuSimulator.Application.Interfaces.Simulation;
using VpuSimulator.Application.Scenario;
using VpuSimulator.Application.Scheduling;
using VpuSimulator.Domain.Enums;

namespace VpuSimulator.Api.Endpoints;

public static partial class ControlApiEndpoints
{
    // GET /api/v1/scenario: 204 nếu chưa nạp (contract 9.10)
    private static IResult GetScenario(ScenarioLoader loader)
    {
        var s = loader.Current;
        return s is null
            ? Results.NoContent()
            : Results.Ok(new ScenarioInfoDto(s.VpuCount, s.VpsCount, s.TaskCount, s.RuleCount, s.Seed, s.LoadedAt));
    }

    // POST /api/v1/scenario[?validate_only=true]
    // validate_only: luôn 200 kèm issues. Nạp thật: 400 nếu có lỗi (không áp dụng gì), 201 nếu áp dụng được.
    private static async Task<IResult> PostScenario(
        ScenarioLoader loader,
        IdentityLifecycleService identities,
        IUnitRegistry units,
        ProfileCatalog catalog,
        IRuleHandlerRegistry registry,
        ILoggerFactory loggerFactory,
        [FromBody] JsonElement body,
        [FromQuery(Name = "validate_only")] bool validateOnly = false,
        CancellationToken cancellationToken = default)
    {
        var plan = ScenarioPlanner.Plan(
            body,
            units.Seed,
            identities.AppIds,
            name => catalog.Contains(name),
            (name, code) => registry.Resolve(name,
                code is { } c && Enum.IsDefined(typeof(AiFlowCode), c) ? (AiFlowCode)c : null) is not null);

        static IReadOnlyList<ScenarioIssueDto> Dto(IEnumerable<ScenarioIssue> issues) =>
            issues.Select(i => new ScenarioIssueDto(i.Severity, i.Title, i.Detail)).ToList();

        if (validateOnly)
        {
            return Results.Ok(new ScenarioValidationDto(Dto(plan.Issues), plan.OkIdentities, plan.OkTasks));
        }

        if (plan.HasErrors)
        {
            return Results.BadRequest(new ScenarioErrorDto(
                "Scenario không hợp lệ, không áp dụng gì cả.", Dto(plan.Issues)));
        }

        // Không truyền token của request: client ngắt giữa chừng không được làm dở việc áp dụng (W-11)
        var applied = await loader.ApplyAsync(plan, CancellationToken.None);
        var all = plan.Issues.Concat(applied.Issues).ToList();

        loggerFactory.CreateLogger("ControlApi").LogInformation(
            "Control API: POST /scenario thêm {Identities} identity, {Tasks} task, {Rules} rule",
            applied.Identities, applied.Tasks, applied.Rules);

        if (applied.Identities == 0)
        {
            return Results.Conflict(new ScenarioErrorDto("Không thêm được identity nào.", Dto(all)));
        }

        return Results.Created("/api/v1/scenario",
            new ScenarioAppliedDto(applied.Identities, applied.Tasks, applied.Rules, Dto(all)));
    }

    // DELETE /api/v1/scenario?confirm=true: gỡ mọi identity (FR-CTL-8, FR-ID-6)
    private static async Task<IResult> DeleteScenario(
        ScenarioLoader loader,
        ILoggerFactory loggerFactory,
        [FromQuery] bool confirm = false)
    {
        if (!confirm)
        {
            return Results.BadRequest(new ErrorDto(
                "Thao tác này gỡ MỌI identity cùng task và unit của chúng; cần thêm tham số confirm=true."));
        }

        var cleared = await loader.ClearAsync(CancellationToken.None);
        loggerFactory.CreateLogger("ControlApi").LogInformation(
            "Control API: DELETE /scenario gỡ {Identities} identity, {Tasks} task", cleared.Identities, cleared.Tasks);
        return Results.Ok(new ScenarioClearedDto(cleared.Identities, cleared.Tasks));
    }
}

