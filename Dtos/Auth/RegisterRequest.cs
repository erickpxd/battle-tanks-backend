using System.ComponentModel.DataAnnotations;

namespace BattleTanks.Backend.Dtos.Auth;

public sealed class RegisterRequest
{
    [Required, MinLength(3), MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? DisplayName { get; set; }
}
