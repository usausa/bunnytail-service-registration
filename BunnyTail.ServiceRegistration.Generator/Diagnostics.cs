namespace BunnyTail.ServiceRegistration.Generator;

using Microsoft.CodeAnalysis;

using SourceGenerateHelper;

internal static class Diagnostics
{
    public static DiagnosticDescriptor InvalidMethodDefinition { get; } = new(
        id: "BTSR0001",
        title: "Invalid method definition",
        messageFormat: "[ServiceRegistration] method must be partial extension without an implementation. method=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor InvalidMethodParameter { get; } = new(
        id: "BTSR0002",
        title: "Invalid method parameter",
        messageFormat: "[ServiceRegistration] parameter type must be IServiceCollection. method=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor InvalidMethodReturnType { get; } = new(
        id: "BTSR0003",
        title: "Invalid method return type",
        messageFormat: "[ServiceRegistration] return type must be IServiceCollection. method=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor InvalidPattern { get; } = new(
        id: "BTSR0004",
        title: "Invalid regex pattern",
        messageFormat: "Pattern is not a valid regex. pattern=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ReferencedAssemblyDisabled { get; } = new(
        id: "BTSR0005",
        title: "Referenced assembly resolution disabled",
        messageFormat: "Referenced assembly is not resolved. assembly=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ConflictingInterfaceRegistration { get; } = new(
        id: "BTSR0006",
        title: "Conflicting interface registration",
        messageFormat: "As and WithInterfaces conflict. pattern=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor PatternNoMatch { get; } = new(
        id: "BTSR0007",
        title: "Pattern matched no type",
        messageFormat: "No type matched the pattern. pattern=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor AsTypeNotImplemented { get; } = new(
        id: "BTSR0008",
        title: "As type not implemented",
        messageFormat: "Class matched by the pattern is not assignable to the As type. class=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor UndefinedLifetime { get; } = new(
        id: "BTSR0009",
        title: "Undefined lifetime",
        messageFormat: "Lifetime is not a defined value. lifetime=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor EmptyPattern { get; } = new(
        id: "BTSR0010",
        title: "Empty pattern",
        messageFormat: "Pattern is empty, which would match every class",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor AssemblyNotReferenced { get; } = new(
        id: "BTSR0011",
        title: "Assembly not referenced",
        messageFormat: "Assembly is not referenced. assembly=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor ClassNotAccessible { get; } = new(
        id: "BTSR0012",
        title: "Class not accessible",
        messageFormat: "Class matched by the pattern is not accessible from the generated code, and is not registered. class=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor InvalidPropertyValue { get; } = new(
        id: "BTSR0013",
        title: "Invalid MSBuild property value",
        messageFormat: "MSBuild property value is not valid, and the default is used. property=[{0}], value=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor HintNameCollision { get; } = new(
        id: "BTSR0014",
        title: "Class name differs only in case",
        messageFormat: "Class name differs only in case from another class, and its registration methods are not generated. class=[{0}], other=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);
}
