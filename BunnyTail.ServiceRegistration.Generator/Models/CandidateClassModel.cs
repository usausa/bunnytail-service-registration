namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record CandidateClassModel(
    string Namespace,
    string Name,
    string FullyQualifiedName,
    bool IsAccessible,
    EquatableArray<InterfaceModel> Interfaces,
    EquatableArray<string> ServiceTypes);
