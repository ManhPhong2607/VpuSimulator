namespace VpuSimulator.Api.Hosting;

public static class ConfigurationExtensions
{
    //FR-CFG-3: bind một mục cấu hình và từ chối khoá không xác định
    public static T GetStrict<T>(this IConfiguration configuration, string section) where T : class, new()
    {
        try
        {
            return configuration.GetSection(section).Get<T>(o => o.ErrorOnUnknownConfiguration = true) ?? new T();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Cấu hình '{section}' không hợp lệ: {ex.Message}", ex);
        }
    }
}