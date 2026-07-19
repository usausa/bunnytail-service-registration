namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record ClassModel(
    string Namespace,
    string ClassName,
    bool IsValueType,
    EquatableArray<MethodRegistrationModel> Methods);
