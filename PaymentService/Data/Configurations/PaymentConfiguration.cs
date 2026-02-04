using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain;

namespace PaymentService.Data.Configurations
{
    public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> b)
        {
            b.ToTable("Payments");

            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OrderId).IsUnique();

            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Currency).HasMaxLength(3);

            b.Property(x => x.Fingerprint).HasMaxLength(128).IsRequired();
            b.Property(x => x.FailureReason).HasMaxLength(64);

            b.Property(x => x.Status).IsRequired();

            b.Property(x => x.CreatedAtUtc).IsRequired();
            b.Property(x => x.UpdatedAtUtc).IsRequired();
        }
    }
}
