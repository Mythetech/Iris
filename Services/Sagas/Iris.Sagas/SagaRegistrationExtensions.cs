using Iris.Sagas.Frameworks;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Sagas;

public static class SagaRegistrationExtensions
{
    public static IServiceCollection AddSagaServices(this IServiceCollection services)
    {
        services.AddSingleton<ISagaDefinitionProvider, MassTransitSagaDefinitionProvider>();
        services.AddSingleton<ISagaSpanMapper, MassTransitSpanMapper>();
        return services;
    }
}
