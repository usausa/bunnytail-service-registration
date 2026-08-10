namespace BunnyTail.ServiceRegistration.Generator;

using Microsoft.CodeAnalysis;

internal static class Diagnostics
{
    public static DiagnosticDescriptor InvalidMethodDefinition { get; } = new(
        id: "BTSR0001",
        title: "Invalid method definition",
        messageFormat: "Method must be partial extension. method=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidMethodParameter { get; } = new(
        id: "BTSR0002",
        title: "Invalid method parameter",
        messageFormat: "Parameter type must be IServiceCollection. method=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidMethodReturnType { get; } = new(
        id: "BTSR0003",
        title: "Invalid method return type",
        messageFormat: "Return type must be IServiceCollection. method=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidPattern { get; } = new(
        id: "BTSR0004",
        title: "Invalid regex pattern",
        messageFormat: "Invalid regex pattern. pattern=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
