using System.ComponentModel.DataAnnotations;
using BattleTanks.Backend.Domain.Enums;

namespace BattleTanks.Backend.Dtos.Players;

public sealed class UpdatePlayerRequest
{
    [Required, MinLength(1), MaxLength(50)]
    public string Nickname { get; set; } = string.Empty;

    [Required]
    public PlayerTeam Team { get; set; } = PlayerTeam.Neutral;

    [Range(0, 1000)]
    public int Health { get; set; } = 100;

    [Range(1, 1000)]
    public int MaxHealth { get; set; } = 100;

    [Range(0, int.MaxValue)]
    public int Score { get; set; }
}
