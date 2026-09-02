using BattleTanks.Contracts.Realtime;
using BattleTanks.GameServer.State;
using FluentAssertions;

namespace BattleTanks.Application.UnitTests.GameServer;

public class InMemoryGameRoomStateTests
{
    [Fact]
    public void Join_ShouldReturnPlayersFromTheSameRoom()
    {
        var state = new InMemoryGameRoomState();

        state.Join("ROOM-ONE", "connection-1", Guid.NewGuid(), "Ana");
        var players = state.Join("ROOM-ONE", "connection-2", Guid.NewGuid(), "Bia");

        players.Select(player => player.Name).Should().BeEquivalentTo("Ana", "Bia");
    }

    [Fact]
    public void Move_ShouldUpdateOnlyTheConnectedPlayer()
    {
        var state = new InMemoryGameRoomState();
        var playerId = Guid.NewGuid();
        state.Join("ROOM-ONE", "connection-1", playerId, "Ana");
        var request = new PlayerMoveRequest(8, 6, "right", "event-1", 1000);

        var player = state.Move("connection-1", request, DateTime.UnixEpoch);

        player.Id.Should().Be(playerId);
        player.Position.Should().Be(new PlayerPosition(8, 6, "right"));
        player.EventId.Should().Be("event-1");
        player.ServerReceivedAt.Should().Be(DateTime.UnixEpoch);
    }

    [Fact]
    public void Leave_ShouldKeepPlayerUntilTheirLastConnectionCloses()
    {
        var state = new InMemoryGameRoomState();
        var playerId = Guid.NewGuid();
        state.Join("ROOM-ONE", "connection-1", playerId, "Ana");
        state.Join("ROOM-ONE", "connection-2", playerId, "Ana");

        state.Leave("connection-1")!.RemainingPlayers.Should().ContainSingle();
        state.Leave("connection-2")!.RemainingPlayers.Should().BeEmpty();
    }
}
