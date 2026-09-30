namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record MethodRegistrationModel(
    string Signature,
    string ParameterName,
    EquatableArray<RegistrationModel> Registrations);
