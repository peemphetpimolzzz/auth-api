using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<TokenResponse>> Register(RegisterRequest request) =>
        StatusCode(StatusCodes.Status201Created, await auth.RegisterAsync(request));

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request) =>
        Ok(await auth.LoginAsync(request));

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request) =>
        Ok(await auth.RefreshAsync(request.RefreshToken));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        await auth.LogoutAsync(request.RefreshToken);
        return NoContent();
    }
}
