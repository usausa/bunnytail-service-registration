namespace BunnyTail.ServiceRegistration.Generator.Models;

internal sealed record OptionModel(
    bool ResolveReferencedAssembly,
    string? InvalidResolveReferencedAssembly,
    string IgnoreInterface);
