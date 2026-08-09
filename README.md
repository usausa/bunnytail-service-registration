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

## Attribute Parameters

| Parameter | Description |
|---|---|
| `Lifetime` | Service lifetime: `Transient`, `Singleton`, or `Scoped` |
| `Pattern` | Regex pattern to match class names to register |
| `Assembly` | Assembly to scan (defaults to the calling assembly) |
| `Namespace` | Namespace prefix to filter classes |

## Note

Registration is resolved by scanning the compilation for types matching each `Pattern`. Because that scan depends on the whole compilation, it re-runs on every edit. The resolved model is equatable, so generated source is only re-emitted when the set of matched registrations actually changes; on a large solution the resolve step itself still runs per edit.
