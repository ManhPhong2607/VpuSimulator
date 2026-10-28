using VpuSimulator.Application.Commands;

namespace VpuSimulator.Application.State;

//Được kho task gọi SAU mỗi thay đổi, ngoài khoá của kho. Không được ném exception ra ngoài
public interface ITaskObserver
{
    //add-task, cntrl-task, update-task: task đã được thêm hoặc sửa
    void TaskApplied(string appId, TaskApplyResult result);

    /// <summary>end-task, stop-all, gỡ identity: các task này đã bị gỡ khỏi kho.</summary>
    void TasksRemoved(string appId, IReadOnlyList<ParsedTask> removed);
}