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

    public static DiagnosticDescriptor ReferencedAssemblyDisabled { get; } = new(
        id: "BTSR0005",
        title: "Referenced assembly resolution disabled",
        messageFormat: "Referenced assembly is not searched. Set ServiceRegistrationResolveReferencedAssembly to true. assembly=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ConflictingInterfaceRegistration { get; } = new(
        id: "BTSR0006",
        title: "Conflicting interface registration",
        messageFormat: "As replaces the service type, so the implementation is not registered and the interface delegate has nothing to resolve. Specify only one of them. pattern=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor PatternNoMatch { get; } = new(
        id: "BTSR0007",
        title: "Pattern matched no type",
        messageFormat: "Pattern matched no type, so nothing is registered. Check the pattern, Namespace and Assembly. pattern=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
