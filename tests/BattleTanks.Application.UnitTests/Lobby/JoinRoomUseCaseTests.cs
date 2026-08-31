using BattleTanks.Application.Abstractions;
using BattleTanks.Application.Lobby;
using BattleTanks.Contracts.Requests.Room;
using BattleTanks.Domain.Entities;
using FluentAssertions;
using Moq;

namespace BattleTanks.Application.UnitTests.Lobby;

public class JoinRoomUseCaseTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly Mock<IPlayerRepository> _playerRepositoryMock;
    private readonly JoinRoomUseCase _sut; // System Under Test

    public JoinRoomUseCaseTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _playerRepositoryMock = new Mock<IPlayerRepository>();
        _sut = new JoinRoomUseCase(_sessionRepositoryMock.Object, _playerRepositoryMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new JoinRoomByCodeRequest { RoomCode = "INVALID" };
        _sessionRepositoryMock
            .Setup(repo => repo.GetByRoomCodeAsync("INVALID", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        // Act
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request);

        // Assert
        result.Found.Should().BeFalse();
        result.Succeeded.Should().BeFalse();
        result.Room.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIsNotWaiting_ReturnsFail()
    {
        // Arrange
        var request = new JoinRoomByCodeRequest { RoomCode = "VALID" };
        var session = new GameSession { RoomCode = "VALID", Status = GameSessionStatus.InGame };
        
        _sessionRepositoryMock
            .Setup(repo => repo.GetByRoomCodeAsync("VALID", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request);

        // Assert
        result.Found.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("This room has already started or finished.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRoomIsFull_ReturnsFail()
    {
        // Arrange
        var request = new JoinRoomByCodeRequest { RoomCode = "VALID" };
        var session = new GameSession { RoomCode = "VALID", Status = GameSessionStatus.Waiting };
        
        // Fill room with 4 players
        for (int i = 0; i < 4; i++)
        {
            session.ConnectedPlayers.Add(new Player { Id = Guid.NewGuid(), Name = $"Player{i}" });
        }
        
        _sessionRepositoryMock
            .Setup(repo => repo.GetByRoomCodeAsync("VALID", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var newPlayerId = Guid.NewGuid();
        var result = await _sut.ExecuteAsync(newPlayerId, request);

        // Assert
        result.Found.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Room is full");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlayerAlreadyInRoom_ReturnsSuccessWithoutAddingAgain()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var request = new JoinRoomByCodeRequest { RoomCode = "VALID" };
        var session = new GameSession { RoomCode = "VALID", Status = GameSessionStatus.Waiting };
        
        session.ConnectedPlayers.Add(new Player { Id = playerId, Name = "ExistingPlayer" });
        
        _sessionRepositoryMock
            .Setup(repo => repo.GetByRoomCodeAsync("VALID", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _sut.ExecuteAsync(playerId, request);

        // Assert
        result.Found.Should().BeTrue();
        result.Succeeded.Should().BeTrue();
        result.Room.Should().NotBeNull();
        result.Room!.Players.Should().Contain("ExistingPlayer");
        
        // Ensure UpdateAsync and SaveChangesAsync were NOT called (it just returns immediately)
        _sessionRepositoryMock.Verify(repo => repo.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionRepositoryMock.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlayerDoesNotExist_ReturnsFail()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var request = new JoinRoomByCodeRequest { RoomCode = "VALID" };
        var session = new GameSession { RoomCode = "VALID", Status = GameSessionStatus.Waiting };
        
        _sessionRepositoryMock
            .Setup(repo => repo.GetByRoomCodeAsync("VALID", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _playerRepositoryMock
            .Setup(repo => repo.GetByIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Player?)null); // Player not found in DB

        // Act
        var result = await _sut.ExecuteAsync(playerId, request);

        // Assert
        result.Found.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("Player not found.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_AddsPlayerToRoomAndReturnsSuccess()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var request = new JoinRoomByCodeRequest { RoomCode = "VALID" };
        var session = new GameSession { Id = Guid.NewGuid(), RoomCode = "VALID", Status = GameSessionStatus.Waiting };
        var player = new Player { Id = playerId, Name = "NewPlayer" };
        
        _sessionRepositoryMock
            .Setup(repo => repo.GetByRoomCodeAsync("VALID", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _playerRepositoryMock
            .Setup(repo => repo.GetByIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);

        // Act
        var result = await _sut.ExecuteAsync(playerId, request);

        // Assert
        result.Found.Should().BeTrue();
        result.Succeeded.Should().BeTrue();
        result.Room.Should().NotBeNull();
        result.Room!.Players.Should().Contain("NewPlayer");
        
        // Check if player's session id was updated
        player.GameSessionId.Should().Be(session.Id);
        session.ConnectedPlayers.Should().Contain(player);
        
        // Ensure we save changes
        _sessionRepositoryMock.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _sessionRepositoryMock.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
