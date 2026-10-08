using System.Reflection;
using NetArchTest.Rules;
using VpuSimulator.Domain;
using Xunit;

namespace VpuSimulator.ArchitectureTests;

/// <summary>
/// Bước 0 (Phan_Chia_Cong_Viec, mục 2): "ArchUnit test — Domain không được reference Infrastructure".
/// Enforce hướng phụ thuộc của solution hiện tại (chỉ đi 1 chiều, vào trong):
///
///   Api ──► Infrastructure ──► Application ──► Domain
///     │            └──────────► Rules ───────► Domain
///     └─────► EventBuilder ──────────────────► Domain
///
/// Test đỏ nghĩa là có người vô tình thêm 1 reference/using sai chiều — sửa code, KHÔNG sửa test.
/// Lưu ý: assembly chưa có type nào (Rules, EventBuilder, Infrastructure lúc mới scaffold) thì
/// test của nó "xanh rỗng" — luật bắt đầu có tác dụng ngay khi có code đầu tiên.
/// </summary>
public class DependencyRuleTests
{
    // Tên namespace gốc của từng project (khớp contract: Domain dùng namespace PHẲNG "VpuSimulator.Domain").
    private const string Domain = "VpuSimulator.Domain";
    private const string Application = "VpuSimulator.Application";
    private const string Infrastructure = "VpuSimulator.Infrastructure";
    private const string Rules = "VpuSimulator.Rules";
    private const string EventBuilder = "VpuSimulator.EventBuilder";
    private const string Api = "VpuSimulator.Api";

    private static readonly Assembly DomainAssembly = typeof(EmissionUnit).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(VpuSimulator.Application.Interfaces.ITickPolicy).Assembly;

    // Project chưa có type nào để lấy bằng typeof(...) → nạp theo tên assembly.
    private static Assembly Load(string name) => Assembly.Load(new AssemblyName(name));

    // ───────────── Domain: lõi, không biết gì về ai khác ─────────────

    [Fact]
    public void Domain_Should_Not_Depend_On_Any_Other_Layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(Application, Infrastructure, Rules, EventBuilder, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Domain_Should_Have_Zero_Third_Party_Dependencies()
    {
        // "Domain Layer — Zero Dependencies": chỉ được dùng thư viện chuẩn .NET (System.*).
        // System.Text.Json (cho RuleContext.RawParams) vẫn hợp lệ vì thuộc framework, không phải NuGet ngoài.
        var external = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                        && !n.StartsWith("netstandard", StringComparison.Ordinal)
                        && !n.StartsWith("mscorlib", StringComparison.Ordinal))
            .ToList();

        Assert.True(external.Count == 0,
            "Domain đang reference assembly ngoài: " + string.Join(", ", external));
    }

    // ───────────── Application: chỉ biết Domain ─────────────

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    // ───────────── Rules & EventBuilder: chỉ biết Domain, và không biết nhau ─────────────

    [Fact]
    public void Rules_Should_Only_Depend_On_Domain()
    {
        var result = Types.InAssembly(Load(Rules))
            .Should()
            .NotHaveDependencyOnAny(Application, Infrastructure, EventBuilder, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void EventBuilder_Should_Only_Depend_On_Domain()
    {
        var result = Types.InAssembly(Load(EventBuilder))
            .Should()
            .NotHaveDependencyOnAny(Application, Infrastructure, Rules, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    // ───────────── Infrastructure: không được "nhìn ngược" lên Api ─────────────

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(Load(Infrastructure))
            .Should()
            .NotHaveDependencyOn(Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result)
    {
        if (result.IsSuccessful || result.FailingTypeNames is null)
        {
            return string.Empty;
        }

        return "Các type vi phạm luật kiến trúc: " + string.Join(", ", result.FailingTypeNames);
    }
}