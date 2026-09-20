using System.Text.Json;
using BattleTanks.Contracts.GameEvents;
using BattleTanks.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BattleTanks.Application.UnitTests.GameEvents;

public sealed class RedisGameEventHistoryTests
{
    [Fact]
    public async Task History_ShouldDeduplicateLimitAndReturnEventsOldestFirst()
    {
        var store = new FakeRedisEventStore();
        var options = new RedisHistoryOptions("unused", 2, TimeSpan.FromMinutes(5));
        var history = new RedisGameEventHistory(
            store,
            options,
            NullLogger<RedisGameEventHistory>.Instance);
        var first = CreateEvent("event-1");
        var second = CreateEvent("event-2");
        var third = CreateEvent("event-3");

        await history.AppendAsync(first);
        await history.AppendAsync(first);
        await history.AppendAsync(second);
        await history.AppendAsync(third);
        var events = await history.GetRecentAsync("room-one");

        events.Select(gameEvent => gameEvent.EventId).Should().Equal("event-2", "event-3");
        store.LastMaxEvents.Should().Be(2);
        store.LastTtl.Should().Be(TimeSpan.FromMinutes(5));
        store.LastEventListKey.Should().Be("battle-tanks:room:ROOM-ONE:events");
    }

    private static GameEventEnvelope CreateEvent(string eventId) => new(
        eventId,
        GameEventTypes.MapTileDestroyed,
        "ROOM-ONE",
        Guid.NewGuid(),
        1000,
        1010,
        JsonSerializer.SerializeToElement(new { column = 2, row = 3 }));

    private sealed class FakeRedisEventStore : IRedisEventStore
    {
        private readonly HashSet<string> _eventIds = [];
        private readonly List<string> _events = [];

        public int LastMaxEvents { get; private set; }
        public TimeSpan LastTtl { get; private set; }
        public string? LastEventListKey { get; private set; }

        public Task<bool> AppendIfNewAsync(
            string eventListKey,
            string deduplicationKey,
            string eventId,
            string serializedEvent,
            int maxEvents,
            TimeSpan timeToLive,
            CancellationToken cancellationToken = default)
        {
            LastEventListKey = eventListKey;
            LastMaxEvents = maxEvents;
            LastTtl = timeToLive;
            if (!_eventIds.Add(eventId))
            {
                return Task.FromResult(false);
            }

            _events.Add(serializedEvent);
            if (_events.Count > maxEvents)
            {
                _events.RemoveRange(0, _events.Count - maxEvents);
            }

            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<string>> ReadAsync(
            string eventListKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(_events.ToArray());
    }
}
