using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IncidentType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.TriggeringObservations).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.EvidenceSummary).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.RuleVersion).HasMaxLength(50);
        builder.HasIndex(x => x.FarmId);
        builder.HasIndex(x => x.FlockId);
        builder.HasIndex(x => x.Status);

        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(x => x.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Flock>()
            .WithMany()
            .HasForeignKey(x => x.FlockId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
