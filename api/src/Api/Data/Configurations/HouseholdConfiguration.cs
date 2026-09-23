using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(50);

        // Tableau PostgreSQL (text[]). Valeur par défaut SQL pour les foyers déjà existants
        // au moment de la migration.
        builder.PrimitiveCollection(h => h.Equipment)
            .HasDefaultValueSql("ARRAY['Hob','Microwave']::text[]")
            .ElementType().HasConversion<string>();

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
