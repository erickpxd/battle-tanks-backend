using System;
using System.Collections.Generic;
using BattleTanks.Contracts.Responses.Chat;

namespace BattleTanks.Application.Chat;

public interface ISimulatedChatRepository
{
    void AddMessage(string roomCode, ChatResponse message);
    IEnumerable<ChatResponse> GetMessages(string roomCode);
}
