namespace BunnyTail.ServiceRegistration;

using SourceGenerateHelper.Testing;

public sealed class PipelineCacheTest
{
    private const string Source =
        """
        using BunnyTail.ServiceRegistration;
        using Microsoft.Extensions.DependencyInjection;

        namespace Test;

        public interface IService
        {
        }

        public sealed class Service : IService
        {
        }

        internal static partial class ServiceCollectionExtensions
        {
            [ServiceRegistration(Lifetime.Singleton, "Service$", WithInterfaces = true)]
            public static partial IServiceCollection AddServices(this IServiceCollection services);
        }
        """;

    private const string UnrelatedSource =
        """
        namespace Other;

        internal sealed class Unrelated;
        """;

    private const string AddedTargetSource =
        """
        namespace Test;

        public sealed class AddedService : IService
        {
        }
        """;

    // ------------------------------------------------------------
    // Cache
    // ------------------------------------------------------------

    [Fact]
    public void UnrelatedEditKeepsModelCached()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental(Source, UnrelatedSource);

        // Assert
        Assert.Equal(result.FirstGeneratedText, result.SecondGeneratedText);
        Assert.NotEmpty(result.OutputReasons);
        Assert.DoesNotContain(result.OutputReasons, static x => x.IsChanged());
    }

    [Fact]
    public void TargetEditRebuildsModel()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental(Source, AddedTargetSource);

        // Assert
        Assert.Contains(result.OutputReasons, static x => x.IsChanged());
    }
}
