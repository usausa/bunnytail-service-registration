namespace BunnyTail.ServiceRegistration;

using System.Collections.Generic;

using BunnyTail.ServiceRegistration.Generator;

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

using SourceGenerateHelper.Testing;

// Driver-based harness for diagnostic scenarios.
// The runtime-behaviour tests build a real ServiceProvider and therefore only reach inputs the
// generator accepts; these cover the refusals.
internal static class GeneratorTestHelper
{
    private static GeneratorTestRunner Runner => GeneratorTestRunner
        .For<ServiceRegistrationGenerator>()
        .WithReference(typeof(ServiceRegistrationAttribute).Assembly)
        .WithReference(typeof(IServiceCollection).Assembly)
        .WithDiagnosticPrefix("BTSR");

    public static IReadOnlyList<Diagnostic> GetDiagnostics(string source) => Runner.GetDiagnostics(source);

    public static IReadOnlyList<Diagnostic> GetDiagnosticsAll(string source) => Runner.GetDiagnosticsAll(source);

    public static string GetGeneratedSource(string source) => Runner.GetGeneratedSource(source);
}
