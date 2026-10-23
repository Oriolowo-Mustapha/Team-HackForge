using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.OwnsOne(x => x.Amount, mc =>
        {
            mc.Property(c => c.Amount).HasColumnName("AmountAmount").HasColumnType("decimal(18,2)");
            mc.Property(c => c.Currency).HasColumnName("AmountCurrency").HasMaxLength(10);
        });
        builder.OwnsOne(x => x.PlatformCommission, mc =>
        {
            mc.Property(c => c.Amount).HasColumnName("PlatformCommissionAmount").HasColumnType("decimal(18,2)");
            mc.Property(c => c.Currency).HasColumnName("PlatformCommissionCurrency").HasMaxLength(10);
        });
        builder.OwnsOne(x => x.TotalAmount, mc =>
        {
            mc.Property(c => c.Amount).HasColumnName("TotalAmountAmount").HasColumnType("decimal(18,2)");
            mc.Property(c => c.Currency).HasColumnName("TotalAmountCurrency").HasMaxLength(10);
        });
        builder.OwnsOne(x => x.VetPayoutAmount, mc =>
        {
            mc.Property(c => c.Amount).HasColumnName("VetPayoutAmountAmount").HasColumnType("decimal(18,2)");
            mc.Property(c => c.Currency).HasColumnName("VetPayoutAmountCurrency").HasMaxLength(10);
        });
        builder.Property(x => x.Status).HasMaxLength(50);
        builder.Property(x => x.Mode).HasMaxLength(20).HasDefaultValue(PaymentMode.Simulated);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(300);
        builder.HasIndex(x => x.ProviderTransactionId);
        builder.Property(x => x.TransactionReference).HasMaxLength(200);
        builder.Property(x => x.CheckoutUrl).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.SubaccountId).HasMaxLength(100);
        builder.HasIndex(x => x.VetRequestId);
        builder.HasIndex(x => x.TransactionReference).IsUnique();
        builder.HasIndex(x => new { x.VetRequestId, x.Status })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
        builder.HasIndex(x => new { x.VetRequestId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        builder.HasOne<VetRequest>()
            .WithMany()
            .HasForeignKey(x => x.VetRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
