using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(p => p.UserId);

        // Supprimer le compte supprime le profil (et donc les données de santé).
        builder.HasOne(p => p.User)
            .WithOne(u => u.Profile)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.CookingTime).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Budget).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Diet).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Goal).HasConversion<string>().HasMaxLength(30);

        // Listes stockées dans un tableau PostgreSQL (text[]) : pas de table de liaison
        // nécessaire pour quelques valeurs fermées.
        builder.PrimitiveCollection(p => p.Exclusions).ElementType().HasConversion<string>();
        builder.PrimitiveCollection(p => p.Allergens).ElementType().HasConversion<string>();

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_UserProfiles_DefaultServings", "\"DefaultServings\" BETWEEN 1 AND 12"));
    }
}
