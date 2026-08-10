namespace BunnyTail.ServiceRegistration;

using Microsoft.CodeAnalysis;

// Diagnostic coverage for ServiceRegistrationGenerator.
// GeneratorTest builds a real ServiceProvider and therefore only reaches inputs the generator
// accepts; these cover the refusals.
public class DiagnosticTest
{
    private const string Head =
        """
        using BunnyTail.ServiceRegistration;
        using Microsoft.Extensions.DependencyInjection;

        """;

    //-----------------------------------------------------------------------
    // BTSR0001 : method must be a static partial extension method
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

    //-----------------------------------------------------------------------
    // BTSR0002 : parameter list must be the extension receiver only
    //-----------------------------------------------------------------------

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

    //-----------------------------------------------------------------------
    // BTSR0003 : return type must be IServiceCollection
    //-----------------------------------------------------------------------

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

    //-----------------------------------------------------------------------
    // BTSR0004 : the pattern must be a valid regular expression
    //-----------------------------------------------------------------------

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
    // Valid input must stay clean
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
