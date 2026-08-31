using System.Collections.Concurrent;
using System.Collections.Generic;
using BattleTanks.Application.Chat;
using BattleTanks.Contracts.Responses.Chat;

namespace BattleTanks.Infrastructure.Persistence.Memory;

public class SimulatedChatRepository : ISimulatedChatRepository
{
    private readonly ConcurrentDictionary<string, List<ChatResponse>> _roomChats = new();

    public void AddMessage(string roomCode, ChatResponse message)
    {
        _roomChats.AddOrUpdate(
            roomCode,
            new List<ChatResponse> { message },
            (key, existingList) =>
            {
                // In a real app we'd need a lock for the list itself, but since this is a simulation for a capstone we just lock the list
                lock (existingList)
                {
                    existingList.Add(message);
                }
                return existingList;
            }
        );
    }

    public IEnumerable<ChatResponse> GetMessages(string roomCode)
    {
        if (_roomChats.TryGetValue(roomCode, out var messages))
        {
            lock (messages)
            {
                return new List<ChatResponse>(messages);
            }
        }
        return Array.Empty<ChatResponse>();
    }
}
