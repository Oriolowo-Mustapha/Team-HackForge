using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class IncidentActionConfiguration : IEntityTypeConfiguration<IncidentAction>
{
    public void Configure(EntityTypeBuilder<IncidentAction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Priority).HasMaxLength(50);
        builder.Property(x => x.Status).HasMaxLength(50);
        builder.HasIndex(x => x.IncidentId);

        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
