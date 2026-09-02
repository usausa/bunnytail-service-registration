namespace Develop;

using BunnyTail.ServiceRegistration;

using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    public static void Main()
    {
        using var provider = new ServiceCollection()
            .AddViews()
            .AddServices()
            .BuildServiceProvider();
    }
}

internal static partial class ServiceCollectionExtensions
{
    [ServiceRegistration(Lifetime.Transient, "View$")]
    [ServiceRegistration(Lifetime.Transient, "ViewModel$")]
    public static partial IServiceCollection AddViews(this IServiceCollection services);

    [ServiceRegistration(Lifetime.Singleton, "Service$", WithInterfaces = true)]
    [ServiceRegistration(Lifetime.Singleton, "Service$", Assembly = "Develop.Library", WithInterfaces = true)]
    public static partial IServiceCollection AddServices(this IServiceCollection services);
}

#pragma warning disable CA1812
internal sealed class TestService;

internal interface INavigation
{
    void OnNavigate();
}

internal sealed class FooView;

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
