using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class VetRequestConfiguration : IEntityTypeConfiguration<VetRequest>
{
    public void Configure(EntityTypeBuilder<VetRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.OwnsOne(x => x.FarmerCost, mc =>
        {
            mc.Property(c => c.Amount).HasColumnName("FarmerCostAmount").HasColumnType("decimal(18,2)");
            mc.Property(c => c.Currency).HasColumnName("FarmerCostCurrency").HasMaxLength(10);
        });
        builder.HasIndex(x => x.IncidentId);
        builder.HasIndex(x => x.Status);
    }
}
