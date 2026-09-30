namespace BunnyTail.ServiceRegistration;

public sealed class GeneratedCodeTests
{
    private const string GeneratedName = "Test_ServiceCollectionExtensions.g.cs";

    // ------------------------------------------------------------
    // Attribute
    // ------------------------------------------------------------

    [Fact]
    public void IncompleteAttributeDoesNotStopOtherMethods()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public sealed class FooService;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton)]
                public static partial IServiceCollection AddMissing(this IServiceCollection services);

                [ServiceRegistration(Lifetime.Singletn, "Service$")]
                public static partial IServiceCollection AddTypo(this IServiceCollection services);

                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run(source);

        // Assert
        Assert.Equal(["CS0117", "CS7036"], result.Problems.Select(static x => x.Id).Order());
        Assert.Contains("AddSingleton<global::Test.FooService>(services)", result.GeneratedSource(GeneratedName), StringComparison.Ordinal);
    }

    [Fact]
    public void MethodDeclarationIsRepeated()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public sealed class FooService;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddGeneric<T>(this IServiceCollection services)
                    where T : class;

                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                internal static partial IServiceCollection AddKeyword(this IServiceCollection @this);

                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection? AddNullable(this IServiceCollection services);
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);

        // Assert
        Assert.Empty(problems);
    }

    // ------------------------------------------------------------
    // Class
    // ------------------------------------------------------------

    [Fact]
    public void ClassNotAccessibleFromRegistrationIsSkipped()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public sealed class Outer
            {
                private sealed class HiddenService;

                public sealed class VisibleService;
            }

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run(source);

        // Assert
        Assert.Equal(["BTSR0012"], result.Problems.Select(static x => x.Id));
        var generated = result.GeneratedSource(GeneratedName);
        Assert.Contains("AddSingleton<global::Test.Outer.VisibleService>(services)", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("HiddenService", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AsTypeImplementedThroughBaseTypeIsRegistered()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public interface IService;

            public abstract class ServiceBase : IService;

            public sealed class FooService : ServiceBase;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$", As = typeof(IService))]
                [ServiceRegistration(Lifetime.Singleton, "Service$", As = typeof(ServiceBase))]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run(source);

        // Assert
        Assert.Empty(result.Problems);
        var generated = result.GeneratedSource(GeneratedName);
        Assert.Contains("AddSingleton<global::Test.IService, global::Test.FooService>(services)", generated, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<global::Test.ServiceBase, global::Test.FooService>(services)", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ObsoleteClassIsRegisteredWithoutWarning()
    {
        // Arrange
        const string source =
            """
            using System;

            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            [Obsolete("old")]
            public sealed class OldService;

            [Obsolete("gone", true)]
            public sealed class GoneService;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run(source);

        // Assert
        Assert.Empty(result.Problems);
        var generated = result.GeneratedSource(GeneratedName);
        Assert.Contains("AddSingleton<global::Test.OldService>(services)", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("GoneService", generated, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------
    // Name
    // ------------------------------------------------------------

    [Fact]
    public void NamespaceNamedMicrosoftDoesNotTakeUsing()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Contoso.Integrations.Microsoft
            {
                public sealed class Marker;
            }

            namespace Contoso.Integrations
            {
                public sealed class FooService;

                internal static partial class ServiceCollectionExtensions
                {
                    [ServiceRegistration(Lifetime.Singleton, "Service$")]
                    public static partial IServiceCollection AddServices(this IServiceCollection services);
                }
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);

        // Assert
        Assert.Empty(problems);
    }

    [Fact]
    public void IgnoreInterfaceAllowsSpaceAfterComma()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public interface IFirst;

            public interface ISecond;

            public interface IThird;

            public sealed class FooService : IFirst, ISecond, IThird;

            internal static partial class ServiceCollectionExtensions
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$", WithInterfaces = true)]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;

        // Act
        var result = GeneratorTestHelper.Run(source, "Test.IFirst, Test.ISecond");

        // Assert
        Assert.Empty(result.Problems);
        var generated = result.GeneratedSource(GeneratedName);
        Assert.DoesNotContain("global::Test.IFirst", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("global::Test.ISecond", generated, StringComparison.Ordinal);
        Assert.Contains("global::Test.IThird", generated, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------
    // Nullable and records
    // ------------------------------------------------------------

    [Fact]
    public void NullableTypeArgumentsCompileWithoutWarning()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public interface IStore<T>;

            public sealed class NameStore : IStore<string?>;

            public interface IKeyed<T>;

            public sealed class KeyedHolder : IKeyed<object?>;

            internal static partial class Registrations
            {
                [ServiceRegistration(Lifetime.Singleton, "Store$", As = typeof(IStore<string?>))]
                public static partial IServiceCollection AddStores(this IServiceCollection services);

                [ServiceRegistration(Lifetime.Singleton, "Holder$", WithInterfaces = true)]
                public static partial IServiceCollection AddHolders(this IServiceCollection services);
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);

        // Assert
        Assert.Empty(problems);
    }

    [Fact]
    public void RecordClassIsRegistered()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public sealed record RecordService;

            internal static partial class Registrations
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;

        // Act
        var generated = GeneratorTestHelper.GetGeneratedSource(source);

        // Assert
        Assert.Contains("AddSingleton<global::Test.RecordService>(services)", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationIsNotTakenByUserExtensionMethod()
    {
        // Arrange
        const string source =
            """
            using BunnyTail.ServiceRegistration;
            using Microsoft.Extensions.DependencyInjection;

            namespace Test;

            public sealed class FooService;

            public static class UserExtensions
            {
                public static IServiceCollection AddSingleton<T>(this IServiceCollection services)
                    where T : class => services;
            }

            internal static partial class Registrations
            {
                [ServiceRegistration(Lifetime.Singleton, "Service$")]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);
        var generated = GeneratorTestHelper.GetGeneratedSource(source);

        // Assert
        Assert.Empty(problems);
        Assert.Contains("global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::Test.FooService>(services)", generated, StringComparison.Ordinal);
    }
}
