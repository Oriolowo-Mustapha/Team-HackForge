using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class LivestockRecordConfiguration : IEntityTypeConfiguration<LivestockRecord>
{
    public void Configure(EntityTypeBuilder<LivestockRecord> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Species).IsRequired().HasMaxLength(50);
        builder.Property(x => x.AnimalName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Sex).HasMaxLength(10);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.FarmId);

        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(x => x.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
