using BattleTanks.Application.Abstractions;
using BattleTanks.Contracts.Responses.Ranking;
using BattleTanks.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BattleTanks.Api.Controllers;

[ApiController]
[Route("api/ranking")]
[Produces("application/json")]
public sealed class RankingController : ControllerBase
{
    private readonly IRankingCache _rankingCache;
    private readonly ApplicationReadDbContext _readDbContext;

    public RankingController(IRankingCache rankingCache, ApplicationReadDbContext readDbContext)
    {
        _rankingCache = rankingCache;
        _readDbContext = readDbContext;
    }

    /// <summary>
    /// Returns the global top-10 ranking from Redis (SortedSet), rebuilding from Postgres if empty.
    /// </summary>
    [HttpGet("top")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RankingEntryResponse>>> GetTop(
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        var cached = await _rankingCache.GetTopAsync(take, cancellationToken);
        if (cached.Count > 0)
        {
            return Ok(cached.Select(Map).ToList());
        }

        var fromDatabase = await LoadFromDatabaseAsync(take, cancellationToken);
        await _rankingCache.RebuildFromAsync(fromDatabase, cancellationToken);
        var rebuilt = await _rankingCache.GetTopAsync(take, cancellationToken);
        return Ok(rebuilt.Select(Map).ToList());
    }

    /// <summary>
    /// Forces a rebuild of the Redis ranking cache from PostgreSQL player totals.
    /// </summary>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var fromDatabase = await LoadFromDatabaseAsync(100, cancellationToken);
        await _rankingCache.RebuildFromAsync(fromDatabase, cancellationToken);
        return NoContent();
    }

    private async Task<List<RankingEntry>> LoadFromDatabaseAsync(int take, CancellationToken cancellationToken)
        => await _readDbContext.Players
            .OrderByDescending(player => player.TotalScore)
            .ThenBy(player => player.Name)
            .Take(Math.Clamp(take, 1, 100))
            .Select(player => new RankingEntry(player.Id, player.Name, player.TotalScore, 0))
            .ToListAsync(cancellationToken);

    private static RankingEntryResponse Map(RankingEntry entry) => new(
        entry.Rank,
        entry.PlayerId,
        entry.PlayerName,
        entry.TotalScore);
}
