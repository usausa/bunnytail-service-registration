namespace BunnyTail.ServiceRegistration;

// Static class — excluded because IsStatic == true
internal static class StaticMatchService
{
}

// Abstract class — excluded because IsAbstract == true
internal abstract class AbstractMatchService
{
}

// Open generic class — excluded because IsGenericType == true
// ReSharper disable once UnusedTypeParameter
internal sealed class GenericMatchService<T>
{
#pragma warning disable IDE0051
#pragma warning disable CA1000
    internal static readonly GenericMatchService<int> Instance = new();
#pragma warning restore CA1000
#pragma warning restore IDE0051
}
