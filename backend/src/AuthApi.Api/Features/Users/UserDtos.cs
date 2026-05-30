namespace AuthApi.Api.Features.Users;

public record MeResponse(Guid Id, string Email, IReadOnlyList<string> Roles);

public record UserSummary(Guid Id, string Email, IReadOnlyList<string> Roles, DateTime CreatedAt);
