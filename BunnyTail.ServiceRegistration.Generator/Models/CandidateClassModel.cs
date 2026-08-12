namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record CandidateClassModel(
    string Namespace,
    string Name,
    string FullyQualifiedName,
    EquatableArray<InterfaceModel> Interfaces);
