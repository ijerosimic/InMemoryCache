using Microsoft.Extensions.DependencyInjection;

namespace InMemoryCache;

public static class ServiceCollectionExtensions
{
    public static void AddInMemoryCache(this IServiceCollection serviceCollection, Action<InMemoryCacheOptions> options)
    {
        if (serviceCollection is null)
            throw new ArgumentNullException(nameof(serviceCollection));

        serviceCollection.Add(ServiceDescriptor.Singleton(typeof(IInMemoryCache<>), typeof(InMemoryCache<>)));
        serviceCollection.Configure<InMemoryCacheOptions>(options.Invoke);
    }
}