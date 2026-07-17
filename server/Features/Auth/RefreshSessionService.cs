using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Auth;

internal sealed record IssuedRefreshSession(string Token, DateTimeOffset ExpiresAtUtc);
internal sealed record RefreshSessionRotationResult(ApplicationUser? User, IssuedRefreshSession? Replacement)
{
    public bool Succeeded => User is not null && Replacement is not null;
}

internal sealed class RefreshSessionService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    UserManager<ApplicationUser> userManager,
    RefreshTokenGenerator tokenGenerator,
    AuthSessionRuntimeConfiguration configuration,
    TimeProvider timeProvider)
{
    public async Task<IssuedRefreshSession> IssueAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return await CreateAsync(user, Guid.NewGuid(), now, now.Add(configuration.RefreshTokenLifetime), cancellationToken);
    }

    public async Task<RefreshSessionRotationResult> RotateAsync(string? rawToken, CancellationToken cancellationToken = default)
    {
        if (!tokenGenerator.IsWellFormed(rawToken))
        {
            return new RefreshSessionRotationResult(null, null);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tokenHash = tokenGenerator.Hash(rawToken!);
        var session = await context.RefreshSessions
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        if (session is null)
        {
            return new RefreshSessionRotationResult(null, null);
        }

        var now = timeProvider.GetUtcNow();
        if (session.RevokedAtUtc is not null || session.ReplacedBySessionId is not null)
        {
            await RevokeFamilyAsync(context, session.TokenFamilyId, now, "Replay detected", cancellationToken);
            return new RefreshSessionRotationResult(null, null);
        }

        if (session.ExpiresAtUtc <= now
            || !session.User.IsActive
            || await userManager.IsLockedOutAsync(session.User)
            || !tokenGenerator.HashesMatch(session.SecurityStampHash, tokenGenerator.Hash(session.User.SecurityStamp ?? string.Empty)))
        {
            await RevokeFamilyAsync(context, session.TokenFamilyId, now, "Session invalid", cancellationToken);
            return new RefreshSessionRotationResult(null, null);
        }

        var replacement = NewSession(session.User, session.TokenFamilyId, session.ExpiresAtUtc, now);
        session.RevokedAtUtc = now;
        session.LastUsedAtUtc = now;
        session.RevocationReason = "Rotated";
        session.ReplacedBySessionId = replacement.Session.Id;
        context.RefreshSessions.Add(replacement.Session);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return new RefreshSessionRotationResult(session.User, new IssuedRefreshSession(replacement.Token, session.ExpiresAtUtc));
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            await RevokeFamilyByIdAsync(session.TokenFamilyId, now, cancellationToken);
            return new RefreshSessionRotationResult(null, null);
        }
    }

    public async Task RevokeFamilyForTokenAsync(string? rawToken, CancellationToken cancellationToken = default)
    {
        if (!tokenGenerator.IsWellFormed(rawToken))
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var tokenHash = tokenGenerator.Hash(rawToken!);
        var familyId = await context.RefreshSessions
            .Where(item => item.TokenHash == tokenHash)
            .Select(item => (Guid?)item.TokenFamilyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (familyId is not null)
        {
            await RevokeFamilyAsync(context, familyId.Value, timeProvider.GetUtcNow(), "Logged out", cancellationToken);
        }
    }

    private async Task<IssuedRefreshSession> CreateAsync(ApplicationUser user, Guid familyId, DateTimeOffset now, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var issued = NewSession(user, familyId, expiresAtUtc, now);
        context.RefreshSessions.Add(issued.Session);
        await context.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshSession(issued.Token, expiresAtUtc);
    }

    private (RefreshSession Session, string Token) NewSession(ApplicationUser user, Guid familyId, DateTimeOffset expiresAtUtc, DateTimeOffset now)
    {
        var token = tokenGenerator.Create();
        return (new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenGenerator.Hash(token),
            TokenFamilyId = familyId,
            SecurityStampHash = tokenGenerator.Hash(user.SecurityStamp ?? string.Empty),
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc
        }, token);
    }

    private static async Task RevokeFamilyAsync(ApplicationDbContext context, Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        foreach (var item in await context.RefreshSessions
                     .Where(session => session.TokenFamilyId == familyId && session.RevokedAtUtc == null)
                     .ToListAsync(cancellationToken))
        {
            item.RevokedAtUtc = now;
            item.RevocationReason = reason;
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeFamilyByIdAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await RevokeFamilyAsync(context, familyId, now, "Replay detected", cancellationToken);
    }
}
