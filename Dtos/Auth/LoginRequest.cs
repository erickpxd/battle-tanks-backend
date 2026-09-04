using System.ComponentModel.DataAnnotations;

namespace BattleTanks.Backend.Dtos.Auth;

public sealed class LoginRequest
{
    [Required]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
