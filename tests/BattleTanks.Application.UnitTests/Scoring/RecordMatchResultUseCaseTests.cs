using BattleTanks.Application.Abstractions;
using BattleTanks.Application.Scoring;
using FluentAssertions;
using Moq;

namespace BattleTanks.Application.UnitTests.Scoring;

public sealed class RecordMatchResultUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldPersistVictoryAndRefreshRankingForAllPlayers()
    {
        var winnerId = Guid.NewGuid();
        var defeatedId = Guid.NewGuid();
        var repository = new Mock<IScoreRepository>();
        var ranking = new Mock<IRankingCache>();
        repository.Setup(item => item.RecordMatchResultAsync(
                "TANK-TEST",
                It.IsAny<IReadOnlyCollection<Guid>>(),
                winnerId,
                RecordMatchResultUseCase.VictoryPoints,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RecordedPlayerScore(winnerId, "Ana", 100, 300, 3, 2),
                new RecordedPlayerScore(defeatedId, "Bia", 0, 50, 3, 1)
            ]);
        var useCase = new RecordMatchResultUseCase(repository.Object, ranking.Object);

        var result = await useCase.ExecuteAsync(
            "TANK-TEST",
            [winnerId, defeatedId],
            winnerId,
            DateTime.UtcNow);

        result.Should().HaveCount(2);
        result.Single(item => item.PlayerId == winnerId).PointsAwarded.Should().Be(100);
        ranking.Verify(item => item.UpsertScoreAsync(
            winnerId,
            "Ana",
            300,
            It.IsAny<CancellationToken>()), Times.Once);
        ranking.Verify(item => item.UpsertScoreAsync(
            defeatedId,
            "Bia",
            50,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
