namespace BunnyTail.ServiceRegistration;

using System.Collections.Generic;

using BunnyTail.ServiceRegistration.Generator;

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

using SourceGenerateHelper.Testing;

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
