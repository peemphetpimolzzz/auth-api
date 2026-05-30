using AuthApi.Api.Auth;
using AuthApi.Api.Common;
using AuthApi.Api.Data;
using AuthApi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Api.Features.Auth;

public class AuthService(AuthDbContext db, IPasswordHasher hasher, ITokenService tokens)
{
    public async Task<TokenResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            throw new ConflictException("An account with that email already exists.");
        }

        var userRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.User)
            ?? throw new InvalidOperationException("The User role has not been seeded.");

        var user = new User
        {
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
        };
        user.UserRoles.Add(new UserRole { Role = userRole });
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return await IssueTokensAsync(user.Id);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedDomainException("Invalid email or password.");
        }

        return await IssueTokensAsync(user.Id);
    }

    public async Task<TokenResponse> RefreshAsync(string rawToken)
    {
        var hash = tokens.HashToken(rawToken);
        var stored = await db.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash)
            ?? throw new UnauthorizedDomainException("Invalid refresh token.");

        if (stored.RevokedAt is not null)
        {
            // Re-use of an already-revoked token signals theft: revoke the whole active set.
            await RevokeAllActiveAsync(stored.UserId);
            throw new UnauthorizedDomainException("Refresh token has been revoked.");
        }
        if (stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedDomainException("Refresh token has expired.");
        }

        var (rawNew, hashNew, expiresAt) = tokens.CreateRefreshToken();
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByTokenHash = hashNew;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = hashNew,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var roles = await GetRolesAsync(stored.UserId);
        var access = tokens.CreateAccessToken(stored.User!, roles);
        return new TokenResponse(access, rawNew, tokens.AccessTokenSeconds);
    }

    public async Task LogoutAsync(string rawToken)
    {
        var hash = tokens.HashToken(rawToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private async Task<TokenResponse> IssueTokensAsync(Guid userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var roles = await GetRolesAsync(userId);
        var (rawNew, hashNew, expiresAt) = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = hashNew,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var access = tokens.CreateAccessToken(user, roles);
        return new TokenResponse(access, rawNew, tokens.AccessTokenSeconds);
    }

    private async Task<IReadOnlyCollection<string>> GetRolesAsync(Guid userId) =>
        await db.UserRoles.Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role!.Name)
            .ToListAsync();

    private async Task RevokeAllActiveAsync(Guid userId)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();
        foreach (var token in active)
        {
            token.RevokedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }
}
