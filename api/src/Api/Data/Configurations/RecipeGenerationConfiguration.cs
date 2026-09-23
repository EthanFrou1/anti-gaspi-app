using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class RecipeGenerationConfiguration : IEntityTypeConfiguration<RecipeGeneration>
{
    public void Configure(EntityTypeBuilder<RecipeGeneration> builder)
    {
        // jsonb : PostgreSQL vérifie que le contenu est un JSON valide.
        builder.Property(r => r.RecipeJson).HasColumnType("jsonb");

        builder.HasOne(r => r.Household)
            .WithMany()
            .HasForeignKey(r => r.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        // Compte supprimé : ses générations disparaissent avec lui.
        builder.HasOne(r => r.RequestedBy)
            .WithMany()
            .HasForeignKey(r => r.RequestedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comptage des quotas du jour (par utilisateur, et global par date) et historique du foyer.
        builder.HasIndex(r => new { r.RequestedByUserId, r.CreatedAt });
        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => new { r.HouseholdId, r.CreatedAt });
    }
}
