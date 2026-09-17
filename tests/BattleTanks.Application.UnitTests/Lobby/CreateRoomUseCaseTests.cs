using BattleTanks.Application.Abstractions;
using BattleTanks.Application.Lobby;
using BattleTanks.Contracts.Requests.Room;
using BattleTanks.Domain.Entities;
using FluentAssertions;
using Moq;

namespace BattleTanks.Application.UnitTests.Lobby;

public sealed class CreateRoomUseCaseTests
{
    [Theory]
    [InlineData("battleground")]
    [InlineData("fortress")]
    [InlineData("swamp")]
    [InlineData("ruins")]
    public async Task ExecuteAsync_ShouldPersistEachSupportedMap(string map)
    {
        var playerId = Guid.NewGuid();
        var sessions = new Mock<IGameSessionRepository>();
        var players = new Mock<IPlayerRepository>();
        players.Setup(repository => repository.GetByIdAsync(
                playerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Player { Id = playerId, Name = "Ana" });
        sessions.Setup(repository => repository.RoomCodeExistsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        GameSession? savedSession = null;
        sessions.Setup(repository => repository.AddAsync(
                It.IsAny<GameSession>(),
                It.IsAny<CancellationToken>()))
            .Callback<GameSession, CancellationToken>((session, _) => savedSession = session)
            .Returns(Task.CompletedTask);
        var useCase = new CreateRoomUseCase(sessions.Object, players.Object);

        var response = await useCase.ExecuteAsync(
            playerId,
            new CreateRoomRequest { MapSelected = map });

        response.MapSelected.Should().Be(map);
        savedSession.Should().NotBeNull();
        savedSession!.MapSelected.Should().Be(map);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectAnUnsupportedMap()
    {
        var playerId = Guid.NewGuid();
        var sessions = new Mock<IGameSessionRepository>();
        var players = new Mock<IPlayerRepository>();
        players.Setup(repository => repository.GetByIdAsync(
                playerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Player { Id = playerId, Name = "Ana" });
        var useCase = new CreateRoomUseCase(sessions.Object, players.Object);

        var action = () => useCase.ExecuteAsync(
            playerId,
            new CreateRoomRequest { MapSelected = "unknown" });

        await action.Should().ThrowAsync<ArgumentException>();
        sessions.Verify(repository => repository.AddAsync(
            It.IsAny<GameSession>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
