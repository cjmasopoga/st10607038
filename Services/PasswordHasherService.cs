using Microsoft.AspNetCore.Identity;
using SmartMonitoringSolutions.Models;

namespace SmartMonitoringSolutions.Services;

/// <summary>
/// Thin wrapper around ASP.NET Core's battle-tested <see cref="PasswordHasher{TUser}"/> so the
/// "simple relational table" of credentials never stores plain-text passwords.
/// </summary>
public class PasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string plainTextPassword) => _hasher.HashPassword(user, plainTextPassword);

    public bool Verify(User user, string plainTextPassword)
    {
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, plainTextPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
