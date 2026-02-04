using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain;

namespace OrderService.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .ValueGeneratedNever();

        b.Property(x => x.UserId)
            .IsRequired();

        b.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(OrderStatus.Unknown);

        b.Property(x => x.FailureReason)
            .HasMaxLength(256)
            .IsUnicode(false);

        b.Property(x => x.PaymentId)
            .IsRequired(false);

        b.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        b.Property(x => x.CompletedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired(false);

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.PaymentId);
    }
}