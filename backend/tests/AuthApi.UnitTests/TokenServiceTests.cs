using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthApi.Api.Auth;
using AuthApi.Api.Domain;
using Microsoft.Extensions.Options;
using Xunit;

namespace AuthApi.UnitTests;

public class TokenServiceTests
{
    private static TokenService CreateService() => new(Options.Create(new JwtOptions
    {
        Key = "unit_test_signing_key_0123456789_abcdef",
        Issuer = "auth-api",
        Audience = "auth-api-clients",
        AccessMinutes = 15,
        RefreshDays = 30,
    }));

    [Fact]
    public void Access_token_carries_subject_email_and_roles()
    {
        var service = CreateService();
        var user = new User { Id = Guid.NewGuid(), Email = "user@demo.dev" };

        var jwt = service.CreateAccessToken(user, new[] { RoleNames.Admin, RoleNames.User });
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        Assert.Equal(user.Id.ToString(), token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "user@demo.dev");
        var roles = token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains(RoleNames.Admin, roles);
        Assert.Contains(RoleNames.User, roles);
    }

    [Fact]
    public void Access_token_expires_in_the_future()
    {
        var service = CreateService();
        var jwt = service.CreateAccessToken(new User { Email = "u@d.dev" }, Array.Empty<string>());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        Assert.True(token.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public void Refresh_tokens_are_unique_and_hash_is_deterministic()
    {
        var service = CreateService();
        var (token1, hash1, _) = service.CreateRefreshToken();
        var (token2, _, _) = service.CreateRefreshToken();

        Assert.NotEqual(token1, token2);
        Assert.NotEqual(token1, hash1);
        Assert.Equal(hash1, service.HashToken(token1));
    }
}
