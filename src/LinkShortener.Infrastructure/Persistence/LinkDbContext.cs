using LinkShortener.Application.Links;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LinkShortener.Infrastructure.Persistence;

public sealed class LinkDbContext(DbContextOptions<LinkDbContext> options) : DbContext(options)
{
    public DbSet<ShortLink> Links => Set<ShortLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var utcDateTimeConverter = new ValueConverter<DateTime, DateTime>(
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        modelBuilder.Entity<ShortLink>(entity =>
        {
            entity.ToTable("Links", table =>
            {
                table.HasCheckConstraint("CK_Links_Code_Length", "length(\"Code\") BETWEEN 4 AND 20");
                table.HasCheckConstraint("CK_Links_DestinationUrl_Length", "length(\"DestinationUrl\") <= 2048");
                table.HasCheckConstraint("CK_Links_ClickCount_NonNegative", "\"ClickCount\" >= 0");
            });
            entity.HasKey(link => link.Id);
            entity.Property(link => link.Code).HasMaxLength(20).IsRequired();
            entity.HasIndex(link => link.Code).IsUnique();
            entity.Property(link => link.DestinationUrl).HasMaxLength(2048).IsRequired();
            entity.Property(link => link.ExpiresAtUtc).HasConversion(utcDateTimeConverter);
            entity.Property(link => link.ClickCount).IsRequired();
        });
    }
}
