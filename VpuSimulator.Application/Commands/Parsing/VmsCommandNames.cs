namespace VpuSimulator.Application.Commands;

/// <summary>Giá trị của trường <c>command</c> trong envelope VMS (SRS 3.1.2). So khớp đúng từng ký tự.</summary>
public static class VmsCommandNames
{
    public const string AddTask = "add-task";
    public const string CntrlTask = "cntrl-task";
    public const string EndTask = "end-task";
    public const string UpdateTask = "update-task";
    public const string StopAll = "stop-all";

    public static bool IsKnown(string? name) =>
        name is AddTask or CntrlTask or EndTask or UpdateTask or StopAll;
}
