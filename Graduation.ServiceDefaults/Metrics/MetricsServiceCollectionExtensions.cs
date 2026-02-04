using Microsoft.Extensions.DependencyInjection;

namespace Graduation.ServiceDefaults.Metrics;

public static class MetricsServiceCollectionExtensions
{
    public static IServiceCollection AddResilienceMetrics(this IServiceCollection services)
    {
        services.AddSingleton<IResilienceMetrics, ResilienceMetrics>();
        return services;
    }
}
