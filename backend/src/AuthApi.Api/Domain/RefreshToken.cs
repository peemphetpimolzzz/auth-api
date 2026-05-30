using System.ComponentModel.DataAnnotations.Schema;

namespace AuthApi.Api.Domain;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>SHA-256 hash of the opaque token; the raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    [NotMapped]
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
