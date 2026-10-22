using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class LivestockCategoryConfiguration : IEntityTypeConfiguration<LivestockCategory>
{
    public void Configure(EntityTypeBuilder<LivestockCategory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => x.FarmId);
        builder.HasIndex(x => x.Name);

        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(x => x.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LivestockAnimalConfiguration : IEntityTypeConfiguration<LivestockAnimal>
{
    public void Configure(EntityTypeBuilder<LivestockAnimal> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Species).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Breed).HasMaxLength(100);
        builder.Property(x => x.Gender).HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.DiseaseHistory).HasMaxLength(1000);
        builder.Property(x => x.Vaccinations).HasMaxLength(500);
        builder.Property(x => x.Parentage).HasMaxLength(500);
        builder.HasIndex(x => x.FarmId);
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.Species);

        builder.HasOne<LivestockCategory>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(x => x.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
