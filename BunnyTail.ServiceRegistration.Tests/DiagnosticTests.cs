namespace BunnyTail.ServiceRegistration;

using System.Globalization;
using System.Reflection;

using BunnyTail.ServiceRegistration.Generator;

using Microsoft.CodeAnalysis;

public class DiagnosticTests
{
    private const string Head =
        """
        using BunnyTail.ServiceRegistration;
        using Microsoft.Extensions.DependencyInjection;

        """;

    // ------------------------------------------------------------
    // Declaration (00xx)
    // ------------------------------------------------------------

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
    public void Btsr0001ImplementedPartialMethodEmitsDiagnostic()
    {
        var problems = GeneratorTestHelper.GetProblemIds(Head +
            """
            namespace Test;

            public sealed class FooService;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);

                public static partial IServiceCollection AddServices(this IServiceCollection services) => services;
            }
            """);

        Assert.Equal(["BTSR0001"], problems);
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
    public void Btsr0004UndefinedLifetimeEmitsDiagnostic()
    {
        var problems = GeneratorTestHelper.GetProblemIds(Head +
            """
            namespace Test;

            public sealed class FooService;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration((Lifetime)5, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        Assert.Equal(["BTSR0004"], problems);
    }

    [Fact]
    public void Btsr0005EmptyPatternEmitsDiagnostic()
    {
        var problems = GeneratorTestHelper.GetProblemIds(Head +
            """
            namespace Test;

            public sealed class FooService;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        Assert.Equal(["BTSR0005"], problems);
    }

    [Fact]
    public void Btsr0006ClassNamesDifferingOnlyInCaseEmitDiagnostic()
    {
        var result = GeneratorTestHelper.Run(Head +
            """
            namespace Test;

            public sealed class FooService;

            internal static partial class Registrations
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }

            internal static partial class registrations
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddOthers(this IServiceCollection services);
            }
            """);

        Assert.Contains(result.Problems, static x => x.Id == "BTSR0006");
        Assert.DoesNotContain(result.Problems, static x => x.Id == "CS8785");
        Assert.Single(result.GeneratedSources);
    }

    [Fact]
    public void Btsr0007InvalidPropertyValueEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnosticsWithOption("ServiceRegistrationResolveReferencedAssembly", "yes", Head +
            """
            namespace Test;

            public sealed class FooService;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BTSR0007", diagnostic.Id);
        Assert.Contains("value=[yes]", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void Btsr0008InvalidPatternEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics(Head +
            """
            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "[")]
                public static partial IServiceCollection AddBroken(this IServiceCollection services);
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTSR0008");
    }

    [Fact]
    public void Btsr0009ConflictingInterfaceRegistrationEmitsDiagnostic()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public interface IService
            {
            }

            public sealed class Service : IService
            {
            }

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$", As = typeof(IService), WithInterfaces = true)]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        // Assert
        Assert.Contains(diagnostics, static x => x.Id == "BTSR0009");
    }

    [Fact]
    public void Btsr0010AssemblyWithoutResolveOptionEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics(Head +
            """
            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$", Assembly = "Develop.Library", WithInterfaces = true)]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTSR0010");
    }

    [Fact]
    public void Btsr0011UnreferencedAssemblyEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnosticsWithReference(Head +
            """
            namespace Test;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$", Assembly = "Not.Referenced")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        Assert.Equal(["BTSR0011"], diagnostics.Select(static x => x.Id));
    }

    // ------------------------------------------------------------
    // Convention matching (01xx)
    // ------------------------------------------------------------

    [Fact]
    public void Btsr0101ClassNotAssignableToAsTypeEmitsDiagnostic()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public interface IService;

            public sealed class FooService : IService;

            public sealed class BarService;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$", As = typeof(IService))]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run(source);

        // Assert
        var problem = Assert.Single(result.Problems);
        Assert.Equal("BTSR0101", problem.Id);
        Assert.True(problem.Location.IsInSource);
        var generated = result.GeneratedSource("Test_ServiceCollectionExtensions.g.cs");
        Assert.Contains("AddSingleton<global::Test.IService, global::Test.FooService>(services)", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("BarService", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Btsr0102InaccessibleClassEmitsDiagnostic()
    {
        var result = GeneratorTestHelper.Run(Head +
            """
            namespace Test;

            public sealed class Outer
            {
                private sealed class HiddenService;

                internal sealed class VisibleService;
            }

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        var diagnostic = Assert.Single(result.Problems);
        Assert.Equal("BTSR0102", diagnostic.Id);
        Assert.Contains("class=[HiddenService]", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.DoesNotContain("HiddenService", result.AllGeneratedText, StringComparison.Ordinal);
    }

    [Fact]
    public void Btsr0103PatternWithNoMatchEmitsDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnostics(Head +
            """
            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "NothingMatchesThis$")]
                public static partial IServiceCollection AddNothing(this IServiceCollection services);
            }
            """);

        Assert.Contains(diagnostics, static x => x.Id == "BTSR0103");
    }

    // ------------------------------------------------------------
    // Valid
    // ------------------------------------------------------------

    [Fact]
    public void AssemblyWithResolveOptionEmitsNoDiagnostic()
    {
        var diagnostics = GeneratorTestHelper.GetDiagnosticsWithReference(Head +
            """
            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$", Assembly = "Develop.Library", WithInterfaces = true)]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void AssemblyWithResolveOptionGeneratesReferenceRegistrations()
    {
        var generated = GeneratorTestHelper.GetGeneratedSourceWithReference(Head +
            """
            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$", Assembly = "Develop.Library", WithInterfaces = true)]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        Assert.Contains("global::Develop.Library.FooService", generated, StringComparison.Ordinal);
        Assert.Contains("global::Develop.Library.IBarService", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("InternalLibraryService", generated, StringComparison.Ordinal);
    }

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
                [ServiceRegistration(Lifetime.Singleton, "Service$", WithInterfaces = true)]
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
                [ServiceRegistration(Lifetime.Singleton, "Service$", WithInterfaces = true)]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        Assert.Contains("AddServices", generated, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------
    // Reporting
    // ------------------------------------------------------------

    [Fact]
    public void DiagnosticIsReportedInSource()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "NothingMatchesThis$")]
                public static partial IServiceCollection AddNothing(this IServiceCollection services);

                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static IServiceCollection AddServices(this IServiceCollection services) => services;
            }
            """;

        // Act
        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        // Assert
        Assert.Equal(["BTSR0001", "BTSR0103"], diagnostics.Select(static x => x.Id).Order());
        Assert.All(diagnostics, static x => Assert.True(x.Location.IsInSource));
    }

    [Fact]
    public void ErrorsCannotBeSuppressed()
    {
        // Arrange
        var descriptors = typeof(ServiceRegistrationGenerator).Assembly.GetType("BunnyTail.ServiceRegistration.Generator.Diagnostics", throwOnError: true)!
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(static x => x.PropertyType == typeof(DiagnosticDescriptor))
            .Select(static x => (DiagnosticDescriptor)x.GetValue(null)!)
            .ToList();

        // Assert
        Assert.All(
            descriptors.Where(static x => x.DefaultSeverity == DiagnosticSeverity.Error),
            static x => Assert.Equal([WellKnownDiagnosticTags.NotConfigurable, WellKnownDiagnosticTags.Compiler], x.CustomTags));
    }
}
