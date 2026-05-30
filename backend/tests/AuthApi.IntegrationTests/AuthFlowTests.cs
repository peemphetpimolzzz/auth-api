using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthApi.Api.Features.Auth;
using AuthApi.Api.Features.Users;
using Xunit;

namespace AuthApi.IntegrationTests;

public class AuthFlowTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient ClientWithToken(string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    [Fact]
    public async Task Register_then_access_me_returns_user_with_role()
    {
        var register = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("user1@demo.dev", "P@ssw0rd123"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var tokens = await register.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);

        var me = await ClientWithToken(tokens!.AccessToken).GetFromJsonAsync<MeResponse>("/api/users/me");
        Assert.Equal("user1@demo.dev", me!.Email);
        Assert.Contains("User", me.Roles);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("user2@demo.dev", "P@ssw0rd123"));

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("user2@demo.dev", "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_old_token_is_rejected()
    {
        var register = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("user3@demo.dev", "P@ssw0rd123"));
        var tokens = await register.Content.ReadFromJsonAsync<TokenResponse>();

        var refresh = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(tokens!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotEqual(tokens.RefreshToken, rotated!.RefreshToken);

        var reuse = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }
}
