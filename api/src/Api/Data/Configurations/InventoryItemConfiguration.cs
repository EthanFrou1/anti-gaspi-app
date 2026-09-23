using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.Property(i => i.Name).HasMaxLength(100);
        builder.Property(i => i.Barcode).HasMaxLength(14);

        // Jusqu'à 9 999 999,999 : assez pour des grammes comme pour des pièces.
        builder.Property(i => i.Quantity).HasPrecision(10, 3);

        builder.Property(i => i.Unit).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);

        // Supprimer le foyer supprime son inventaire (règle « foyer vide supprimé avec son contenu »).
        builder.HasOne(i => i.Household)
            .WithMany()
            .HasForeignKey(i => i.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Category)
            .WithMany()
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Compte supprimé : ses produits perso deviennent communs (la nourriture reste au frigo).
        builder.HasOne(i => i.Owner)
            .WithMany()
            .HasForeignKey(i => i.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Requête principale : les produits actifs d'un foyer, triés par date de péremption.
        builder.HasIndex(i => new { i.HouseholdId, i.Status, i.ExpiresOn });

        builder.ToTable(t => t.HasCheckConstraint("CK_InventoryItems_Quantity", "\"Quantity\" > 0"));
    }
}
