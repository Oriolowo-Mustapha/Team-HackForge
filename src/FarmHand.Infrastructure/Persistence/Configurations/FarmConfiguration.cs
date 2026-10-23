using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class FarmConfiguration : IEntityTypeConfiguration<Farm>
{
    public void Configure(EntityTypeBuilder<Farm> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OwnerId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Location).HasMaxLength(300);
        builder.Property(x => x.Latitude);
        builder.Property(x => x.Longitude);
        builder.Property(x => x.CityOrTown).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.LocationSource).HasMaxLength(50);
        builder.HasIndex(x => x.OwnerId);
    }
}
