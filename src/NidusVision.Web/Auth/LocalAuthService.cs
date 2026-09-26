using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Models;
using NidusVision.Data;

namespace NidusVision.Web.Auth;

public sealed class LocalAuthService(AppDbContext db, PasswordHasher<LocalUser> hasher)
{
    /// <summary>Serializes the exists-check and insert so two first-run requests cannot both create an admin.</summary>
    private static readonly SemaphoreSlim SetupGate = new(1, 1);

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) =>
        await db.LocalUsers.AnyAsync(cancellationToken);

    public async Task SetupAsync(string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentOutOfRangeException.ThrowIfLessThan(password.Length, 8);

        await SetupGate.WaitAsync(cancellationToken);
        try
        {
            if (await db.LocalUsers.AnyAsync(cancellationToken))
            {
                throw new InvalidOperationException("Password is already configured.");
            }

            var user = new LocalUser { PasswordHash = "" };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.LocalUsers.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            SetupGate.Release();
        }
    }

    public async Task<bool> VerifyAsync(string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var user = await db.LocalUsers.OrderBy(u => u.Id).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return false;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is not PasswordVerificationResult.Failed;
    }
}
