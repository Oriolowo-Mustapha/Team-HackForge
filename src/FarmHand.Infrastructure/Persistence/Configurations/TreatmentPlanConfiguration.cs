using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class TreatmentPlanConfiguration : IEntityTypeConfiguration<TreatmentPlan>
{
    public void Configure(EntityTypeBuilder<TreatmentPlan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AuthoredByVetId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.InstructionsText).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Language).HasMaxLength(20);
        builder.HasIndex(x => x.VetRequestId);
    }
}
