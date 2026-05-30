namespace AuthApi.Api.Auth;

public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessMinutes { get; set; } = 15;
    public int RefreshDays { get; set; } = 30;
}
