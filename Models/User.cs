using System.ComponentModel.DataAnnotations;

namespace SmartMonitoringSolutions.Models;

/// <summary>
/// Simple relational user account record: login credentials plus an assigned system role
/// (Farmer, Agronomist, or Admin). Stored in the "Users" table in Azure SQL Database.
/// </summary>
public class User
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    /// <summary>Securely hashed password (never store plain text). See <see cref="Services.PasswordHasherService"/>.</summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; } = UserRole.Farmer;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
