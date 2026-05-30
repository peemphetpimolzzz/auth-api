using AuthApi.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthApi.Api.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(256);
        builder.HasIndex(t => t.TokenHash);
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(256);

        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
