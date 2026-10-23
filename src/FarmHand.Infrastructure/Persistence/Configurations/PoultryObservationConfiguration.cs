using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class PoultryObservationConfiguration : IEntityTypeConfiguration<PoultryObservation>
{
    public void Configure(EntityTypeBuilder<PoultryObservation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WaterConsumption).HasMaxLength(100);
        builder.Property(x => x.FeedConsumption).HasMaxLength(100);
        builder.Property(x => x.EggProduction).HasMaxLength(100);
        builder.Property(x => x.FarmerNotes).HasMaxLength(1000);
        builder.Property(x => x.RawTranscript).HasMaxLength(2000);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.VisionAnalysis).HasMaxLength(2000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(x => x.FarmId);
        builder.HasIndex(x => x.LivestockAnimalId);
        builder.HasIndex(x => x.LivestockCategoryId);
        builder.HasIndex(x => x.RecordedAt);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(x => x.FarmId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<LivestockAnimal>()
            .WithMany()
            .HasForeignKey(x => x.LivestockAnimalId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<LivestockCategory>()
            .WithMany()
            .HasForeignKey(x => x.LivestockCategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
