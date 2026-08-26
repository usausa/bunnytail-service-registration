namespace BunnyTail.ServiceRegistration;

using System;
using System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ServiceRegistrationAttribute : Attribute
{
    public Lifetime Lifetime { get; }

    public string Pattern { get; }

    public string Assembly { get; set; } = default!;

    public string Namespace { get; set; } = default!;

    public Type? As { get; set; }

    public bool WithInterfaces { get; set; }

    public ServiceRegistrationAttribute(Lifetime lifetime, [StringSyntax(StringSyntaxAttribute.Regex)] string pattern)
    {
        Lifetime = lifetime;
        Pattern = pattern;
    }
}
