using BattleTanks.GameServer.State;
using Microsoft.Extensions.DependencyInjection;

namespace BattleTanks.GameServer;

public static class DependencyInjection
{
    public static IServiceCollection AddGameServer(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IGameRoomState, InMemoryGameRoomState>();
        return services;
    }
}
