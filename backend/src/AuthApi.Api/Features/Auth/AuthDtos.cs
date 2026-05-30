using System.ComponentModel.DataAnnotations;

namespace AuthApi.Api.Features.Auth;

public record RegisterRequest([Required, EmailAddress] string Email, [Required, MinLength(8)] string Password);

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record TokenResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds);
