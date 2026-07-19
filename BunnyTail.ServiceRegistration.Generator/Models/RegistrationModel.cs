namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record RegistrationModel(
    string ServiceTypeName,
    EquatableArray<string> InterfaceTypeNames,
    int Lifetime);
