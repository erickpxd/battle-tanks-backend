using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BattleTanks.Domain.Entities;

public enum GameSessionStatus
{
    Waiting,
    InGame,
    Finished
}

public class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [MaxLength(255)]
    public string MapSelected { get; set; } = string.Empty;
    
    public GameSessionStatus Status { get; set; } = GameSessionStatus.Waiting;
    
    public ICollection<Player> ConnectedPlayers { get; set; } = new List<Player>();
}
