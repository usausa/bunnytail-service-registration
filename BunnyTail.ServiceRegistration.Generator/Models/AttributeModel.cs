namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record AttributeModel(
    int Lifetime,
    bool IsLifetimeDefined,
    string Pattern,
    string Assembly,
    string Namespace,
    string? AsType,
    string? AsTypeName,
    bool WithInterfaces,
    LocationInfo? Location);
