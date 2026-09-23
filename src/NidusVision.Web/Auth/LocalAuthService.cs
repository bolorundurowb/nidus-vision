using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Models;
using NidusVision.Data;

namespace NidusVision.Web.Auth;

public sealed class LocalAuthService(AppDbContext db, PasswordHasher<LocalUser> hasher)
{
    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) =>
        await db.LocalUsers.AnyAsync(cancellationToken);

    public async Task SetupAsync(string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentOutOfRangeException.ThrowIfLessThan(password.Length, 8);

        if (await db.LocalUsers.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("Password is already configured.");
        }

        var user = new LocalUser { PasswordHash = "" };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.LocalUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> VerifyAsync(string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var user = await db.LocalUsers.FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return false;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is not PasswordVerificationResult.Failed;
    }
}
