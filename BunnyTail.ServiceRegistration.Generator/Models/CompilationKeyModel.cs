namespace BunnyTail.ServiceRegistration.Generator.Models;

using Microsoft.CodeAnalysis;

internal sealed record CompilationKeyModel(
    string? AssemblyName,
    CompilationOptions Options);
