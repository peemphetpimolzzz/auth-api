using System.Security.Claims;
using AuthApi.Api.Data;
using AuthApi.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Api.Features.Users;

[ApiController]
[Route("api/users")]
public class UsersController(AuthDbContext db) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public ActionResult<MeResponse> Me()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException();
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return Ok(new MeResponse(Guid.Parse(idValue), email, roles));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("admin/users")]
    public async Task<ActionResult<IReadOnlyList<UserSummary>>> AllUsers()
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserSummary(
                u.Id,
                u.Email,
                u.UserRoles.Select(ur => ur.Role!.Name).ToList(),
                u.CreatedAt))
            .ToListAsync();
        return Ok(users);
    }
}
