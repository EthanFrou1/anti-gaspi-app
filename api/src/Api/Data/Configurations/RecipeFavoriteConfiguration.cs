using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class RecipeFavoriteConfiguration : IEntityTypeConfiguration<RecipeFavorite>
{
    public void Configure(EntityTypeBuilder<RecipeFavorite> builder)
    {
        // Une seule étoile par utilisateur et par recette (ajouter deux fois ne fait rien).
        builder.HasKey(f => new { f.RecipeGenerationId, f.UserId });

        builder.HasOne(f => f.Recipe)
            .WithMany(r => r.Favorites)
            .HasForeignKey(f => f.RecipeGenerationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comptage du plafond de 200 favoris par utilisateur.
        builder.HasIndex(f => f.UserId);
    }
}
