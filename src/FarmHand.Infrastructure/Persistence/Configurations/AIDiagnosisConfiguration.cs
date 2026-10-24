using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class AIDiagnosisConfiguration : IEntityTypeConfiguration<AIDiagnosis>
{
    public void Configure(EntityTypeBuilder<AIDiagnosis> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Severity).IsRequired();
        builder.Property(x => x.DiagnosisSummary).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.RecommendedAction).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.FarmerActionsJson).HasMaxLength(2000);
        builder.Property(x => x.RawAssessment).HasMaxLength(4000);
        builder.Property(x => x.Source).HasMaxLength(100);
        builder.HasIndex(x => x.ObservationId).IsUnique();
        builder.HasIndex(x => x.IncidentId);
        builder.HasIndex(x => x.Severity);
    }
}
