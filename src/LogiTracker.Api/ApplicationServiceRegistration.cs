using LogiTracker.Application.Deliveries.AssignCourier;
using LogiTracker.Application.Deliveries.CreateDelivery;
using LogiTracker.Application.Deliveries.GetDelivery;
using LogiTracker.Application.Deliveries.ReleaseCourier;
using LogiTracker.Application.Outbox;

namespace LogiTracker.Api;

/// <summary>Composición: el punto de entrada es el único lugar que conoce todas las capas.</summary>
internal static class ApplicationServiceRegistration
{
    public static IServiceCollection AddLogiTrackerApplication(this IServiceCollection services)
    {
        services.AddScoped<AssignCourierUseCase>();
        services.AddScoped<ReleaseCourierUseCase>();
        services.AddScoped<CreateDeliveryUseCase>();
        services.AddScoped<GetDeliveryQuery>();
        services.AddScoped<DispatchOutboxUseCase>();
        return services;
    }
}
