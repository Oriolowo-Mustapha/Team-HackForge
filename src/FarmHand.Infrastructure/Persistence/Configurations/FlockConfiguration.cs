using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class FlockConfiguration : IEntityTypeConfiguration<Flock>
{
    public void Configure(EntityTypeBuilder<Flock> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FlockName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.BirdType).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.FarmId);

        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(x => x.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
