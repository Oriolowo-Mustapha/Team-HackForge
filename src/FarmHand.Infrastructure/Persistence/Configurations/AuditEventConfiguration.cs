using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Details).HasMaxLength(1000);
        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => x.EventType);
    }
}
