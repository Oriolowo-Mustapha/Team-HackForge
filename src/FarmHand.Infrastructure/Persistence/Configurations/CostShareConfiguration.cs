using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class CostShareConfiguration : IEntityTypeConfiguration<CostShare>
{
    public void Configure(EntityTypeBuilder<CostShare> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PayerType).IsRequired().HasMaxLength(50);
        builder.OwnsOne(x => x.Amount, mc =>
        {
            mc.Property(c => c.Amount).HasColumnName("AmountAmount").HasColumnType("decimal(18,2)");
            mc.Property(c => c.Currency).HasColumnName("AmountCurrency").HasMaxLength(10);
        });
        builder.HasIndex(x => x.VetRequestId);
    }
}
