using FarmHand.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmHand.Infrastructure.Persistence.Configurations;

public class VetPartnerConfiguration : IEntityTypeConfiguration<VetPartner>
{
    public void Configure(EntityTypeBuilder<VetPartner> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Specialization).HasMaxLength(100);
        builder.Property(x => x.Contact).HasMaxLength(200);
        builder.Property(x => x.PayoutBankCode).HasMaxLength(20);
        builder.Property(x => x.PayoutAccountNumber).HasMaxLength(50);
        builder.Property(x => x.PayoutAccountName).HasMaxLength(150);
        builder.Property(x => x.PayoutCountry).HasMaxLength(10);
        builder.Property(x => x.PaymentSubaccountId).HasMaxLength(100);
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
