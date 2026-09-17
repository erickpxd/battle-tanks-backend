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

        var result = state.Move("connection-1", request, DateTime.UnixEpoch);

        result.WasThrottled.Should().BeFalse();
        result.Player.Id.Should().Be(playerId);
        result.Player.Position.Should().Be(new PlayerPosition(8, 6, "right"));
        result.Player.EventId.Should().Be("event-1");
        result.Player.ServerReceivedAt.Should().Be(DateTime.UnixEpoch);
    }

    [Fact]
    public void Move_ShouldThrottleEventsArrivingFasterThan30Ms()
    {
        var state = new InMemoryGameRoomState();
        state.Join("ROOM-ONE", "connection-1", Guid.NewGuid(), "Ana");

        // First move at t=1000ms — should be accepted.
        var first = state.Move("connection-1",
            new PlayerMoveRequest(1, 1, "right", "event-1", ClientSentAtUnixMs: 1000),
            DateTime.UnixEpoch);

        // Second move at t=1010ms (delta=10ms < 30ms) — should be throttled.
        var throttled = state.Move("connection-1",
            new PlayerMoveRequest(2, 2, "down", "event-2", ClientSentAtUnixMs: 1010),
            DateTime.UnixEpoch);

        // Third move at t=1040ms (delta=40ms >= 30ms) — should be accepted.
        var accepted = state.Move("connection-1",
            new PlayerMoveRequest(3, 3, "left", "event-3", ClientSentAtUnixMs: 1040),
            DateTime.UnixEpoch);

        first.WasThrottled.Should().BeFalse();
        throttled.WasThrottled.Should().BeTrue();
        accepted.WasThrottled.Should().BeFalse();
        accepted.Player.Position.Should().Be(new PlayerPosition(3, 3, "left"));
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

    [Fact]
    public void Hit_ShouldDamageTheTargetPlayerAndKeepTheShooterUnchanged()
    {
        var state = new InMemoryGameRoomState();
        var shooterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        state.Join("ROOM-ONE", "connection-1", shooterId, "Ana");
        state.Join("ROOM-ONE", "connection-2", targetId, "Bia");
        var request = new PlayerHitRequest(targetId, 25, "hit-1", 1000);

        var target = state.Hit("connection-1", request, DateTime.UnixEpoch);

        target.Id.Should().Be(targetId);
        target.Health.Should().Be(75);
        target.Status.Should().Be("active");
        target.EventId.Should().Be("hit-1");
    }

    [Fact]
    public void Hit_ShouldDestroyTheTargetAtZeroHealth()
    {
        var state = new InMemoryGameRoomState();
        var targetId = Guid.NewGuid();
        state.Join("ROOM-ONE", "connection-1", Guid.NewGuid(), "Ana");
        state.Join("ROOM-ONE", "connection-2", targetId, "Bia");

        var target = state.Hit(
            "connection-1",
            new PlayerHitRequest(targetId, 100, "hit-1", 1000),
            DateTime.UnixEpoch);

        target.Health.Should().Be(0);
        target.Status.Should().Be("destroyed");
        state.GetPlayers("ROOM-ONE").Count(player => player.Status == "active").Should().Be(1);
    }

    [Fact]
    public void Hit_ShouldApplyTheSameEventIdOnlyOnce()
    {
        var state = new InMemoryGameRoomState();
        var targetId = Guid.NewGuid();
        state.Join("ROOM-ONE", "connection-1", Guid.NewGuid(), "Ana");
        state.Join("ROOM-ONE", "connection-2", targetId, "Bia");
        var request = new PlayerHitRequest(targetId, 25, "hit-once", 1000);

        state.Hit("connection-1", request, DateTime.UnixEpoch);
        var duplicateResult = state.Hit("connection-1", request, DateTime.UnixEpoch);

        duplicateResult.Health.Should().Be(75);
    }

    [Fact]
    public void PowerUp_ShouldSpawnAtOneRandomPredefinedPointAndBeCollectedOnlyOnce()
    {
        var state = new InMemoryGameRoomState();
        var playerId = Guid.NewGuid();
        state.Join("ROOM-ONE", "connection-1", playerId, "Ana");
        var now = DateTime.UtcNow.AddMinutes(1);

        var update = state.UpdatePowerUps("ROOM-ONE", now);

        update.Changed.Should().BeTrue();
        var powerUp = update.ActivePowerUps.Should().ContainSingle().Subject;
        PowerUpTypes.All.Should().Contain(powerUp.Type);

        state.Move(
            "connection-1",
            new PlayerMoveRequest(powerUp.X, powerUp.Y, "down", "move-power-up", 1000),
            now);
        var result = state.CollectPowerUp(
            "connection-1",
            new CollectPowerUpRequest(powerUp.Id, "collect-1", 1001),
            now);

        result.ActivePowerUps.Should().BeEmpty();
        result.Player.Id.Should().Be(playerId);
        Action duplicateCollection = () => state.CollectPowerUp(
            "connection-1",
            new CollectPowerUpRequest(powerUp.Id, "collect-2", 1002),
            now);
        duplicateCollection.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Shield_ShouldAbsorbDamageBeforeHealth()
    {
        var state = new InMemoryGameRoomState();
        var shooterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        state.Join("ROOM-SHIELD", "shooter", shooterId, "Ana");
        state.Join("ROOM-SHIELD", "target", targetId, "Bia");
        var now = DateTime.UtcNow.AddMinutes(1);

        // Spawn/collect until the deterministic room lifecycle offers a shield.
        RealtimePlayer? shielded = null;
        for (var attempt = 0; attempt < 100 && shielded is null; attempt++)
        {
            var powerUp = state.UpdatePowerUps("ROOM-SHIELD", now.AddMinutes(attempt))
                .ActivePowerUps.Single();
            state.Move(
                "target",
                new PlayerMoveRequest(powerUp.X, powerUp.Y, "down", $"move-{attempt}", 1000 + attempt * 100),
                now.AddMinutes(attempt));
            var collected = state.CollectPowerUp(
                "target",
                new CollectPowerUpRequest(powerUp.Id, $"collect-{attempt}", 1001 + attempt * 100),
                now.AddMinutes(attempt));
            if (powerUp.Type == PowerUpTypes.Shield)
            {
                shielded = collected.Player;
            }
        }

        shielded.Should().NotBeNull();
        var hit = state.Hit(
            "shooter",
            new PlayerHitRequest(targetId, 25, "shield-hit", 20000),
            shielded!.ShieldExpiresAt!.Value.AddSeconds(-1));
        hit.Health.Should().Be(100);
        hit.ShieldHealth.Should().Be(25);
    }
}
