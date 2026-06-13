namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record AttributeModel(
    int Lifetime,
    string Pattern,
    string Assembly,
    string Namespace,
    LocationInfo? Location);
