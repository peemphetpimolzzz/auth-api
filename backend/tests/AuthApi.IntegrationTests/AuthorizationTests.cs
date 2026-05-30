using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthApi.Api.Data;
using AuthApi.Api.Features.Auth;
using Xunit;

namespace AuthApi.IntegrationTests;

public class AuthorizationTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string> TokenForAsync(string email, string password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        return tokens!.AccessToken;
    }

    private HttpClient ClientWithToken(string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    [Fact]
    public async Task Admin_endpoint_requires_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/users/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Regular_user_is_forbidden_from_admin_endpoint()
    {
        await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("plain@demo.dev", "P@ssw0rd123"));
        var token = await TokenForAsync("plain@demo.dev", "P@ssw0rd123");

        var response = await ClientWithToken(token).GetAsync("/api/users/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Seeded_admin_can_access_admin_endpoint()
    {
        var token = await TokenForAsync(DbSeeder.AdminEmail, DbSeeder.AdminPassword);
        var response = await ClientWithToken(token).GetAsync("/api/users/admin/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
