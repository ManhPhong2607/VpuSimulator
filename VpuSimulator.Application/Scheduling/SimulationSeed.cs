using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace VpuSimulator.Application.Scheduling;

/// <summary>
/// Seed mô phỏng. Mỗi unit có Random riêng, seed suy ra từ seed chung và khoá của unit
/// bằng hash ổn định: cùng đầu vào thì luôn ra cùng seed, kể cả giữa các lần chạy và các máy
/// Tuyệt đối không dùng string.GetHashCode() ở đây: .NET đổi giá trị đó mỗi lần khởi động tiến trình
/// </summary>
public static class SimulationSeed
{
    public const string PayloadStream = "payload";
    public const string ScheduleStream = "schedule";
    //Seed cấu hình 0 nghĩa là chọn seed ngẫu nhiên. Trả về seed thực tế, để ghi log và báo qua API
    public static long Resolve(long configured)
    {
        if (configured < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configured), "seed phải là số không âm");
        }

        return configured != 0 ? configured : RandomNumberGenerator.GetInt32(1, int.MaxValue);
    }

    /// <summary>
    /// Seed của một unit. Mỗi trường được ghi kèm độ dài nên ("ab","c") và ("a","bc") cho kết quả khác nhau
    /// Không phụ thuộc thứ tự tạo unit
    /// </summary>
    public static int ForUnit(long seed, string appId, string taskId, int roiId, string ruleName, int epoch, string stream)
    {
        ArgumentNullException.ThrowIfNull(appId);
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(ruleName);
        ArgumentNullException.ThrowIfNull(stream);

        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(seed);
            writer.Write(appId);
            writer.Write(taskId);
            writer.Write(roiId);
            writer.Write(ruleName);
            writer.Write(epoch);
            writer.Write(stream);
        }

        var hash = SHA256.HashData(memory.ToArray());
        return BinaryPrimitives.ReadInt32LittleEndian(hash);
    }

    public static Random CreateRandom(long seed, string appId, string taskId, int roiId, string ruleName, int epoch, string stream) =>
        new(ForUnit(seed, appId, taskId, roiId, ruleName, epoch, stream));
}