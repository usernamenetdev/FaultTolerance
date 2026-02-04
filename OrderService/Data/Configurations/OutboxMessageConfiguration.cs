using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain;

namespace OrderService.Data.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.OutboxType)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.UserId)
            .HasMaxLength(64) // под X-User-Id (строка). Если точно GUID — можешь сделать 36.
            .IsUnicode(false)
            .IsRequired();

        b.Property(x => x.Attempts)
            .IsRequired()
            .HasDefaultValue(0);

        b.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(OutboxStatus.Pending);

        b.Property(x => x.NextAttemptUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        b.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Критический индекс под выборку диспетчером:
        // WHERE Status = Pending AND NextAttemptUtc <= now ORDER BY CreatedAtUtc
        b.HasIndex(x => new { x.Status, x.NextAttemptUtc });

        b.HasIndex(x => x.CreatedAtUtc);
    }
}