namespace BunnyTail.ServiceRegistration.Generator.Models;

using SourceGenerateHelper;

internal sealed record ResolvedRegistrationModel(
    EquatableArray<ClassModel> Classes,
    EquatableArray<DiagnosticInfo> Diagnostics);
