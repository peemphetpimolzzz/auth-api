using AuthApi.Api.Auth;
using AuthApi.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthApi.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("RUN_MIGRATIONS", "false");
        builder.UseSetting("SEED_DATA", "false");
        builder.UseEnvironment("Production");
    }

    /// <summary>Recreate a clean schema and seed roles + the admin account before each test class.</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db, hasher);
    }
}
