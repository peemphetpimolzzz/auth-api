using AuthApi.Api.Domain;

namespace AuthApi.Api.Auth;

public interface ITokenService
{
    string CreateAccessToken(User user, IReadOnlyCollection<string> roles);

    /// <summary>Returns the raw token (given to the client) plus its hash and expiry (stored).</summary>
    (string Token, string Hash, DateTime ExpiresAt) CreateRefreshToken();

    string HashToken(string token);

    int AccessTokenSeconds { get; }
}
