using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class TreatmentScheduleItemConfiguration : IEntityTypeConfiguration<TreatmentScheduleItem>
{
    public void Configure(EntityTypeBuilder<TreatmentScheduleItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.TreatmentPlanId);

        builder.HasOne<TreatmentPlan>()
            .WithMany()
            .HasForeignKey(x => x.TreatmentPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
