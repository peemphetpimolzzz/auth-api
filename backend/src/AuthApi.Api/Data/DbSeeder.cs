using AuthApi.Api.Auth;
using AuthApi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Api.Data;

/// <summary>
/// Seeds the Admin/User roles and one admin account. Idempotent: safe to run on every start.
/// </summary>
public static class DbSeeder
{
    public const string AdminEmail = "admin@demo.dev";
    public const string AdminPassword = "Admin123!";

    public static async Task SeedAsync(AuthDbContext db, IPasswordHasher hasher)
    {
        var seededAt = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

        foreach (var name in new[] { RoleNames.Admin, RoleNames.User })
        {
            if (!await db.Roles.AnyAsync(r => r.Name == name))
            {
                db.Roles.Add(new Role { Name = name });
            }
        }
        await db.SaveChangesAsync();

        if (!await db.Users.AnyAsync(u => u.Email == AdminEmail))
        {
            var admin = new User
            {
                Email = AdminEmail,
                PasswordHash = hasher.Hash(AdminPassword),
                CreatedAt = seededAt,
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            var roleIds = await db.Roles
                .Where(r => r.Name == RoleNames.Admin || r.Name == RoleNames.User)
                .Select(r => r.Id)
                .ToListAsync();
            foreach (var roleId in roleIds)
            {
                db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = roleId });
            }
            await db.SaveChangesAsync();
        }
    }
}
