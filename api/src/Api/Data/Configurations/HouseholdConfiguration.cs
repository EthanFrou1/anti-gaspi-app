using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(50);

        // Supprimer un foyer supprime tout son contenu (membres, invitations,
        // et plus tard l'inventaire).
        builder.HasMany(h => h.Members)
            .WithOne(m => m.Household)
            .HasForeignKey(m => m.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(h => h.Invitations)
            .WithOne(i => i.Household)
            .HasForeignKey(i => i.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
