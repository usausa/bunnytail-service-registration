namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record RegistrationModel(
    string ServiceTypeName,
    EquatableArray<string> InterfaceTypeNames,
    string? AsType,
    bool WithInterfaces,
    int Lifetime);
