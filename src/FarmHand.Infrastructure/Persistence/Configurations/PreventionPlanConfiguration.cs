using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class PreventionPlanConfiguration : IEntityTypeConfiguration<PreventionPlan>
{
    public void Configure(EntityTypeBuilder<PreventionPlan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Content).IsRequired().HasMaxLength(2000);
        builder.HasIndex(x => x.IncidentId);
    }
}
