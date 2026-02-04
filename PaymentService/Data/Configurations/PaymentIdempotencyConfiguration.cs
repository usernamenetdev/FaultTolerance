using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain;

namespace PaymentService.Data.Configurations
{
    public sealed class PaymentIdempotencyConfiguration : IEntityTypeConfiguration<PaymentIdempotency>
    {
        public void Configure(EntityTypeBuilder<PaymentIdempotency> b)
        {
            b.ToTable("PaymentIdempotency");

            b.HasKey(x => x.IdempotencyKey);

            b.Property(x => x.IdempotencyKey).HasMaxLength(64).IsRequired();
            b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();

            b.Property(x => x.CreatedAtUtc).IsRequired();
        }
    }
}
