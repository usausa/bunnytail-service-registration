# BunnyTail.ServiceRegistration

| Package | Info |
|:-|:-|
| BunnyTail.ServiceRegistration | [![NuGet](https://img.shields.io/nuget/v/BunnyTail.ServiceRegistration.svg)](https://www.nuget.org/packages/BunnyTail.ServiceRegistration) |

## What is this?

Service registration method generator.

## Usage

```csharp
using BunnyTail.ServiceRegistration;

using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    public static void Main()
    {
        using var provider = new ServiceCollection()
            .AddServices()
            .BuildServiceProvider();

        var service = provider.GetRequiredService<TestService>();
    }
}

internal static partial class ServiceCollectionExtensions
{
    [ServiceRegistration(Lifetime.Singleton, "Service$")]
    public static partial IServiceCollection AddServices(this IServiceCollection services);
}

internal sealed class TestService
{
}
```

## Registration Shapes

| Attribute | Generated registration per match |
|---|---|
| `[ServiceRegistration(Lifetime.Transient, "View$")]` | `AddTransient<Impl>()` |
| `[ServiceRegistration(Lifetime.Transient, "Handler$", As = typeof(IHandler))]` | `AddTransient<IHandler, Impl>()` |
| `[ServiceRegistration(Lifetime.Singleton, "Service$", WithInterfaces = true)]` | `AddSingleton<Impl>()` and `AddSingleton<IFoo>(x => x.GetRequiredService<Impl>())` per interface |

## Attribute Parameters

| Parameter | Description |
|---|---|
| `Lifetime` | Service lifetime: `Transient`, `Singleton`, or `Scoped` |
| `Pattern` | Regex pattern to match class names to register. Matching no type is reported as warning BTSR0007 |
| `Assembly` | Assembly to scan (defaults to the calling assembly) |
| `Namespace` | Namespace prefix to filter classes |
| `As` | Service type applied to every matched class, replacing the implementation type |
| `WithInterfaces` | Also register each directly declared interface as a delegate to the implementation. Default `false` |

## MSBuild Properties

| Property | Default | Description |
|---|---|---|
| `ServiceRegistrationResolveReferencedAssembly` | `false` | Enable scanning of referenced assemblies specified by the `Assembly` parameter. When disabled, only the containing assembly is scanned and `Assembly` usage is reported as warning BTSR0005 |
| `ServiceRegistrationIgnoreInterface` | (none) | Comma-separated interface names to exclude from registration. `System.IDisposable` and `System.IAsyncDisposable` are always excluded |

```xml
<PropertyGroup>
  <ServiceRegistrationResolveReferencedAssembly>true</ServiceRegistrationResolveReferencedAssembly>
  <ServiceRegistrationIgnoreInterface>MyApp.INavigation</ServiceRegistrationIgnoreInterface>
</PropertyGroup>
```
