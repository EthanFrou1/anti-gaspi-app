using Api.Data.Seed;
using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class OffCategoryMappingConfiguration : IEntityTypeConfiguration<OffCategoryMapping>
{
    public void Configure(EntityTypeBuilder<OffCategoryMapping> builder)
    {
        builder.HasKey(m => m.OffTag);
        builder.Property(m => m.OffTag).HasMaxLength(100);

        // Une catégorie encore référencée ne peut pas être supprimée par erreur.
        builder.HasOne(m => m.Category)
            .WithMany()
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(CategorySeed.OffMappings);
    }
}
