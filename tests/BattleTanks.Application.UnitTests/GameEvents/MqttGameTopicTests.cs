using System.Text.Json;
using BattleTanks.Contracts.GameEvents;
using BattleTanks.Infrastructure.Messaging;
using FluentAssertions;

namespace BattleTanks.Application.UnitTests.GameEvents;

public sealed class MqttGameTopicTests
{
    [Theory]
    [InlineData(GameEventTypes.PlayerHit, "battle-tanks/ROOM-ONE/events/collisions/player-hit")]
    [InlineData(GameEventTypes.MapTileDestroyed, "battle-tanks/ROOM-ONE/events/map/tile-destroyed")]
    [InlineData(GameEventTypes.GameEnded, "battle-tanks/ROOM-ONE/events/game/ended")]
    public void For_ShouldBuildRoomScopedTopic(string eventType, string expected)
    {
        var gameEvent = new GameEventEnvelope(
            "event-1", eventType, " room-one ", null, 1000, 1010,
            JsonSerializer.SerializeToElement(new { }));

        MqttGameTopic.For(gameEvent).Should().Be(expected);
        MqttGameTopic.RoomSubscription(gameEvent.RoomId)
            .Should().Be("battle-tanks/ROOM-ONE/events/#");
    }
}
