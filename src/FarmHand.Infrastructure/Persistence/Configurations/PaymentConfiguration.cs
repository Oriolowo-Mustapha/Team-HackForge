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
        builder.Property(x => x.Status).HasMaxLength(50);
        builder.Property(x => x.TransactionReference).HasMaxLength(200);
        builder.HasIndex(x => x.VetRequestId);
    }
}
