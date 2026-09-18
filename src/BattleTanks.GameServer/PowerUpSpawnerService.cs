using BattleTanks.GameServer.State;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace BattleTanks.GameServer;

public sealed class PowerUpSpawnerService(
    IGameRoomState gameRoomState,
    IHubContext<GameHub> hubContext) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var updates = gameRoomState.UpdateAllPowerUps(DateTime.UtcNow);
            foreach (var update in updates)
            {
                await hubContext.Clients.Group(update.RoomCode).SendAsync(
                    "ReceivePowerUpsUpdated",
                    update.ActivePowerUps,
                    stoppingToken);
            }
        }
    }
}
