namespace VpuSimulator.Application.Scheduling;

public enum ProfileOutcome
{
    Done,
    NotFound,
    Duplicate,   // đã có profile tên này
    Invalid,     // dữ liệu sai (Message liệt kê lỗi) hoặc thao tác không áp dụng được
    Protected,   // profile mặc định hệ thống hoặc profile có sẵn: không xoá được
    InUse,       // đang có unit hoặc identity dùng: không xoá được
}

public sealed record ProfileResult(
    ProfileOutcome Outcome,
    SimulationProfile? Profile = null,
    string? Message = null,
    int UnitsUpdated = 0);

// Endpoint chỉ việc đổi kết quả thành mã HTTP
public sealed class ProfileService(ProfileCatalog catalog, IUnitRegistry units)
{
    // Các thao tác sửa kho chạy lần lượt, tránh hai yêu cầu xen vào giữa việc kiểm tra và việc ghi
    private readonly object _gate = new();

    public ProfileResult Create(string name, ProfileInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        var nameError = ProfileNameRules.Check(name);
        if (nameError is not null)
        {
            return new ProfileResult(ProfileOutcome.Invalid, Message: nameError);
        }

        if (!input.TryBuild(name, out var profile, out var errors))
        {
            return Invalid(errors);
        }

        lock (_gate)
        {
            return catalog.TryAdd(profile!)
                ? new ProfileResult(ProfileOutcome.Done, profile)
                : new ProfileResult(ProfileOutcome.Duplicate, Message: $"Profile '{name}' đã tồn tại.");
        }
    }

    // Ghi đè TOÀN BỘ định nghĩa và cập nhật luôn các unit đang dùng (quyết định D2)
    public ProfileResult Update(string name, ProfileInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.TryBuild(name, out var profile, out var errors))
        {
            return Invalid(errors);
        }

        lock (_gate)
        {
            if (!catalog.TryReplace(profile!))
            {
                return NotFound(name);
            }

            var updated = units.RefreshProfile(profile!);
            return new ProfileResult(ProfileOutcome.Done, profile, UnitsUpdated: updated);
        }
    }

    public ProfileResult Delete(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            if (!catalog.Contains(name))
            {
                return NotFound(name);
            }

            if (catalog.IsSystemDefault(name))
            {
                return new ProfileResult(ProfileOutcome.Protected,
                    Message: $"Profile '{name}' là profile mặc định của hệ thống, không xoá được.");
            }

            if (ProfileCatalog.IsBuiltin(name))
            {
                return new ProfileResult(ProfileOutcome.Protected,
                    Message: $"Profile '{name}' là profile có sẵn, không xoá được. Dùng reset để khôi phục giá trị gốc.");
            }

            var usage = units.UsageOfProfile(name);
            if (usage.Units > 0 || usage.Identities > 0)
            {
                return new ProfileResult(ProfileOutcome.InUse,
                    Message: $"Profile '{name}' đang được dùng bởi {usage.Units} unit và {usage.Identities} identity. " +
                             "Đổi profile của chúng trước khi xoá.");
            }

            catalog.TryRemove(name);
            return new ProfileResult(ProfileOutcome.Done);
        }
    }

    // Khôi phục giá trị gốc theo SRS FR-SIM-6 (quyết định D4). Chỉ normal và storm
    public ProfileResult Reset(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            if (!catalog.Contains(name))
            {
                return NotFound(name);
            }

            // Tên có tồn tại nhưng là profile tuỳ chỉnh: không có "giá trị gốc" để khôi phục
            if (!SimulationProfiles.Defaults.TryGetValue(name, out var original))
            {
                return new ProfileResult(ProfileOutcome.Invalid,
                    Message: $"Chỉ khôi phục được profile có sẵn ({string.Join(", ", SimulationProfiles.Defaults.Keys)}).");
            }

            catalog.TryReplace(original);
            var updated = units.RefreshProfile(original);
            return new ProfileResult(ProfileOutcome.Done, original, UnitsUpdated: updated);
        }
    }

    private static ProfileResult Invalid(IReadOnlyList<string> errors) =>
        new(ProfileOutcome.Invalid, Message: string.Join("; ", errors));

    private static ProfileResult NotFound(string name) =>
        new(ProfileOutcome.NotFound, Message: $"Không có profile '{name}'.");
}