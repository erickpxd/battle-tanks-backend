using System.Text.Json;
using BattleTanks.Application.GameEvents;
using BattleTanks.Contracts.GameEvents;
using Moq;

namespace BattleTanks.Application.UnitTests.GameEvents;

public sealed class GameEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ShouldPersistAndPublishWithRequestedQos()
    {
        var publisher = new Mock<IGameEventPublisher>();
        var history = new Mock<IGameEventHistory>();
        var dispatcher = new GameEventDispatcher(publisher.Object, history.Object);
        var gameEvent = CreateEvent("event-1");

        await dispatcher.DispatchAsync(gameEvent, GameEventQos.AtLeastOnce);

        history.Verify(service => service.AppendAsync(gameEvent, It.IsAny<CancellationToken>()),
            Times.Once);
        publisher.Verify(service => service.PublishAsync(
            gameEvent,
            GameEventQos.AtLeastOnce,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GameEventEnvelope CreateEvent(string eventId) => new(
        eventId,
        GameEventTypes.PlayerHit,
        "ROOM-ONE",
        Guid.NewGuid(),
        1000,
        1010,
        JsonSerializer.SerializeToElement(new { damage = 25 }));
}
