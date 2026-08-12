namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record ReferenceAssemblyModel(
    string AssemblyName,
    EquatableArray<CandidateClassModel> Classes);
