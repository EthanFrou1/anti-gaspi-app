using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.HasKey(m => new { m.HouseholdId, m.UserId });

        // Règle MVP « un seul foyer par utilisateur », garantie par la base
        // elle-même (index unique), en plus de la vérification dans le service.
        builder.HasIndex(m => m.UserId).IsUnique();

        builder.HasOne(m => m.User)
            .WithOne(u => u.Membership)
            .HasForeignKey<HouseholdMember>(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Enum stocké en texte : lisible en base et robuste si on réordonne l'enum.
        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}
