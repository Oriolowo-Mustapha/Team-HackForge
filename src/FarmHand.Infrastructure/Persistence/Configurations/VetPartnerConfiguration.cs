using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class VetPartnerConfiguration : IEntityTypeConfiguration<VetPartner>
{
    public void Configure(EntityTypeBuilder<VetPartner> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Specialization).HasMaxLength(100);
        builder.Property(x => x.Contact).HasMaxLength(200);
    }
}
