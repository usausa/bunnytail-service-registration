namespace BunnyTail.ServiceRegistration;

// Marker interface used to verify ServiceRegistrationIgnoreInterface suppresses interface registration
// The concrete class must still be resolvable, but IMarkerIgnored itself must not be registered
internal interface IMarkerIgnored;

// Implements IMarkerIgnored — concrete type should be registered, interface should be ignored
internal sealed class MarkerIgnoredService : IMarkerIgnored
{
#pragma warning disable IDE0051
    internal static readonly MarkerIgnoredService Instance = new();
#pragma warning restore IDE0051
}
