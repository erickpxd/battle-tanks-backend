using BattleTanks.Application.Abstractions;
using BattleTanks.Application.GameEvents;
using BattleTanks.Application.Identity;
using BattleTanks.Application.Lobby;
using Microsoft.Extensions.DependencyInjection;

namespace BattleTanks.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Identity use cases
        services.AddScoped<RegisterPlayerUseCase>();
        services.AddScoped<LoginPlayerUseCase>();

        // Lobby use cases
        services.AddScoped<ListRoomsUseCase>();
        services.AddScoped<CreateRoomUseCase>();
        services.AddScoped<JoinRoomUseCase>();

        services.AddSingleton<GameEventDispatcher>();

        return services;
    }
}
