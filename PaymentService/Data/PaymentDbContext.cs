using Microsoft.EntityFrameworkCore;
using PaymentService.Domain;

namespace PaymentService.Data
{
    public sealed class PaymentDbContext : DbContext
    {
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<PaymentIdempotency> PaymentIdempotency => Set<PaymentIdempotency>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
        }
    }
}
