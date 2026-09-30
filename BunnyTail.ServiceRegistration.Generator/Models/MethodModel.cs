namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record MethodModel(
    string Namespace,
    string ClassName,
    bool IsValueType,
    string Signature,
    string ParameterName,
    EquatableArray<AttributeModel> Attributes);
