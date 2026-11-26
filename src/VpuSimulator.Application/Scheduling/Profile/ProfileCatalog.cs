namespace VpuSimulator.Application.Scheduling;

// Kho profile lúc runtime: khởi tạo từ cấu hình đã gộp (có sẵn + khai báo thêm), sau đó thêm/sửa/xoá được qua API.
// Nhiều luồng cùng dùng nên mọi thao tác nằm trong lock. Chỉ lưu trong bộ nhớ (quyết định D1).
public sealed class ProfileCatalog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SimulationProfile> _profiles;

    public ProfileCatalog(SimulationSettings settings)
        : this(settings.Profiles, settings.DefaultProfile.Name)
    {
    }

    public ProfileCatalog(IReadOnlyDictionary<string, SimulationProfile> profiles, string defaultName)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(defaultName);
        if (!profiles.ContainsKey(defaultName))
            throw new ArgumentException($"Profile mặc định '{defaultName}' không có trong danh sách.", nameof(defaultName));

        // Sao chép sang từ điển riêng: sửa kho không được làm đổi dữ liệu gốc (ví dụ SimulationProfiles.Defaults)
        _profiles = new Dictionary<string, SimulationProfile>(profiles, StringComparer.Ordinal);
        DefaultName = defaultName;
    }

    // Tên profile mặc định của hệ thống (scheduler.default_profile). Không đổi lúc chạy.
    public string DefaultName { get; }

    public SimulationProfile? Get(string name)
    {
        lock (_gate)
        {
            return _profiles.GetValueOrDefault(name);
        }
    }

    public bool Contains(string name)
    {
        lock (_gate)
        {
            return _profiles.ContainsKey(name);
        }
    }

    // Sắp theo tên để kết quả ổn định
    public IReadOnlyList<SimulationProfile> All()
    {
        lock (_gate)
        {
            return _profiles.Values.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        }
    }

    public IReadOnlyList<string> Names()
    {
        lock (_gate)
        {
            return _profiles.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        }
    }

    // "Có sẵn" = hai profile theo FR-SIM-6 (normal, storm)
    public static bool IsBuiltin(string name) => SimulationProfiles.Defaults.ContainsKey(name);

    public bool IsSystemDefault(string name) => string.Equals(name, DefaultName, StringComparison.Ordinal);

    // Thêm mới. false nếu đã có tên này (kể cả trùng tên profile có sẵn)
    public bool TryAdd(SimulationProfile profile)
    {
        lock (_gate)
        {
            return _profiles.TryAdd(profile.Name, profile);
        }
    }

    // Ghi đè định nghĩa của profile đã có. false nếu chưa có
    public bool TryReplace(SimulationProfile profile)
    {
        lock (_gate)
        {
            if (!_profiles.ContainsKey(profile.Name)) return false;
            _profiles[profile.Name] = profile;
            return true;
        }
    }

    public bool TryRemove(string name)
    {
        lock (_gate)
        {
            return _profiles.Remove(name);
        }
    }
}