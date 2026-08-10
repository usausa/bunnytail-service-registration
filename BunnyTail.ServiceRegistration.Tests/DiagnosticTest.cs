namespace BunnyTail.ServiceRegistration;

public class DiagnosticTest
{
    private const string Head =
        """
        using BunnyTail.ServiceRegistration;
        using Microsoft.Extensions.DependencyInjection;

        """;

    //-----------------------------------------------------------------------
    // BTSR
    //-----------------------------------------------------------------------

    [Fact]
    public void Btsr0001NonPartialMethodEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics(Head +
            """
            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton)]
                public static IServiceCollection AddServices(this IServiceCollection services) => services;
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTSR0001");
    }

    [Fact]
    public void Btsr0002ExtraParameterEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics(Head +
            """
            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton)]
                public static partial IServiceCollection AddServices(this IServiceCollection services, int value);
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTSR0002");
    }

    [Fact]
    public void Btsr0003InvalidReturnTypeEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics(Head +
            """
            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton)]
                public static partial int AddServices(this IServiceCollection services);
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTSR0003");
    }

    [Fact]
    public void Btsr0004InvalidPatternEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics(Head +
            """
            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "[")]
                public static partial IServiceCollection AddBroken(this IServiceCollection services);
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTSR0004");
    }

    //-----------------------------------------------------------------------
    // Valid
    //-----------------------------------------------------------------------

    [Fact]
    public void ValidRegistrationEmitsNoDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics(Head +
            """
            namespace Test;

            public interface IService
            {
            }

            public sealed class Service : IService
            {
            }

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidRegistrationGeneratesSource()
    {
        var generated = GeneratorTestHelper.GetGeneratedSource(Head +
            """
            namespace Test;

            public interface IService
            {
            }

            public sealed class Service : IService
            {
            }

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        Assert.Contains("AddServices", generated, StringComparison.Ordinal);
    }
}
