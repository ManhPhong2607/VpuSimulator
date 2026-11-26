namespace VpuSimulator.Application.Scheduling;

// Luật đặt tên cho profile MỚI (POST /profiles). Tên nằm trong đường dẫn /profiles/{name}, nên chỉ cho phép ký tự an toàn.
// Cố ý dùng ASCII thuần (giống AppIdRules): chữ có dấu và ký tự Unicode giống mặt chữ không lọt vào.
public static class ProfileNameRules
{
    public const int MaxLength = 64;

    // Trả null nếu hợp lệ, ngược lại trả lý do
    public static string? Check(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "tên profile rỗng";
        if (name.Length > MaxLength)
            return $"tên profile tối đa {MaxLength} ký tự";
        if (!name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            return $"tên profile '{name}' không hợp lệ: chỉ gồm chữ cái a-z, A-Z, chữ số, '-' và '_'";
        return null;
    }
}