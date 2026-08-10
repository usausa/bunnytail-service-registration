namespace BunnyTail.ServiceRegistration;

using Develop.Library;

using Microsoft.Extensions.DependencyInjection;

public class GeneratorTest
{
    [Fact]
    public void TestService()
    {
        using var provider = new ServiceCollection()
            .AddViews()
            .AddServices()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<TestService>());
        Assert.NotNull(provider.GetService<FooService>());
        Assert.NotNull(provider.GetService<IBarService>());
        Assert.NotNull(provider.GetService<IMixedService1>());
        Assert.NotNull(provider.GetService<IMixedService2>());
        Assert.NotNull(provider.GetService<DisposalService>());
        Assert.NotNull(provider.GetService<IBazService>());

        Assert.NotNull(provider.GetService<FooViewModel>());
        Assert.NotNull(provider.GetService<BarViewModel>());
        Assert.Empty(provider.GetServices<INavigation>());

        Assert.Equal(provider.GetService<TestService>(), provider.GetService<TestService>());
    }

    [Fact]
    public void ReferenceAssemblyScanResolvesDevelopLibraryPublicServices()
    {
        // Reference assembly scan: Develop.Library public services must be resolvable.
        using var provider = new ServiceCollection()
            .AddServices()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<FooService>());
        Assert.NotNull(provider.GetService<IBarService>());
        Assert.NotNull(provider.GetService<IMixedService1>());
        Assert.NotNull(provider.GetService<IMixedService2>());
        Assert.NotNull(provider.GetService<DisposalService>());
        Assert.NotNull(provider.GetService<IBazService>());
    }

    [Fact]
    public void DisposableInterfaceIsNotRegisteredAsService()
    {
        using var provider = new ServiceCollection()
            .AddServices()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<DisposalService>());
        Assert.NotNull(provider.GetService<IBazService>());

        Assert.Null(provider.GetService<IDisposable>());
        Assert.Null(provider.GetService<IAsyncDisposable>());
    }

    [Fact]
    public void NamespaceFilterResolvesOnlyMatchingNamespace()
    {
        // Namespace filter: only Sub1 should be registered; Sub2 must be absent.
        using var provider = new ServiceCollection()
            .AddNamespaceFilteredServices()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<Sub1.NamespaceTargetService>());
        Assert.Null(provider.GetService<Sub2.NamespaceTargetService>());
    }

    [Fact]
    public void ForwardingRegistrationSingletonInstanceIsShared()
    {
        // Forwarding registration: concrete type and both interfaces must resolve to the same instance.
        using var provider = new ServiceCollection()
            .AddServices()
            .BuildServiceProvider();

        var concrete = provider.GetRequiredService<MixedService>();
        var via1 = provider.GetRequiredService<IMixedService1>();
        var via2 = provider.GetRequiredService<IMixedService2>();

        Assert.Same(concrete, via1);
        Assert.Same(concrete, via2);
    }

    [Fact]
    public void IgnoreInterfaceConcreteResolvedButInterfaceNotRegistered()
    {
        // ServiceRegistrationIgnoreInterface: IMarkerIgnored must not be registered; concrete must be.
        using var provider = new ServiceCollection()
            .AddServices()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<MarkerIgnoredService>());
        Assert.Null(provider.GetService<IMarkerIgnored>());
    }

    [Fact]
    public void ClassFilterExcludesStaticAbstractAndGenericTypes()
    {
        // Static, abstract, and open-generic types matching "Service$" must not be registered.
        using var provider = new ServiceCollection()
            .AddServices()
            .BuildServiceProvider();

        Assert.Null(provider.GetService<AbstractMatchService>());
        Assert.Null(provider.GetService<GenericMatchService<int>>());
    }
}

internal static partial class ServiceCollectionExtensions
{
    [ServiceRegistration(Lifetime.Transient, "View$")]
    [ServiceRegistration(Lifetime.Transient, "ViewModel$")]
    public static partial IServiceCollection AddViews(this IServiceCollection services);

    [ServiceRegistration(Lifetime.Singleton, "Service$")]
    [ServiceRegistration(Lifetime.Singleton, "Service$", Assembly = "Develop.Library")]
    public static partial IServiceCollection AddServices(this IServiceCollection services);

    [ServiceRegistration(Lifetime.Singleton, "Service$", Namespace = "BunnyTail.ServiceRegistration.Sub1")]
    public static partial IServiceCollection AddNamespaceFilteredServices(this IServiceCollection services);
}

#pragma warning disable CA1812
// ReSharper disable ClassNeverInstantiated.Global
internal sealed class TestService
{
}

internal interface INavigation
{
#pragma warning disable IDE0051
    void OnNavigate();
#pragma warning restore IDE0051
}

internal sealed class FooViewModel : INavigation
{
    public void OnNavigate()
    {
    }
}

internal sealed class BarViewModel : INavigation
{
    public void OnNavigate()
    {
    }
}
// ReSharper restore ClassNeverInstantiated.Global
#pragma warning restore CA1812
