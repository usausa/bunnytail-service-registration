namespace BunnyTail.ServiceRegistration.Generator.Models;

using Microsoft.CodeAnalysis;

using SourceGenerateHelper;

internal sealed record MethodRegistrationModel(
    Accessibility MethodAccessibility,
    string MethodName,
    string ParameterName,
    EquatableArray<RegistrationModel> Registrations);
