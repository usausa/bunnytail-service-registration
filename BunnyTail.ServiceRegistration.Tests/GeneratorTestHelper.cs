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

    private static GeneratorTestRunner ReferenceRunner => Runner
        .WithReference(typeof(Develop.Library.FooService).Assembly)
        .WithGlobalOption("build_property.ServiceRegistrationResolveReferencedAssembly", "true");

    public static IReadOnlyList<Diagnostic> GetDiagnostics(string source) => Runner.GetDiagnostics(source);

    public static IReadOnlyList<Diagnostic> GetDiagnosticsAll(string source) => Runner.GetDiagnosticsAll(source);

    public static string GetGeneratedSource(string source) => Runner.GetGeneratedSource(source);

    public static IReadOnlyList<Diagnostic> GetDiagnosticsWithReference(string source) => ReferenceRunner.GetDiagnostics(source);

    public static string GetGeneratedSourceWithReference(string source) => ReferenceRunner.GetGeneratedSource(source);
}
