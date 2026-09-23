using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class HouseholdInvitationConfiguration : IEntityTypeConfiguration<HouseholdInvitation>
{
    public void Configure(EntityTypeBuilder<HouseholdInvitation> builder)
    {
        builder.Property(i => i.Code).HasMaxLength(16);
        builder.HasIndex(i => i.Code).IsUnique();

        // Si l'auteur supprime son compte, l'invitation reste valable pour le foyer.
        builder.HasOne(i => i.CreatedByUser)
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
