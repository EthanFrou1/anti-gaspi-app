using Api.Data.Seed;
using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        // Identifiants fixés par les données de référence : pas d'auto-incrément.
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Code).HasMaxLength(50);
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.Property(c => c.ExpiryKind).HasConversion<string>().HasMaxLength(20);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ProductCategories_DefaultShelfLifeDays", "\"DefaultShelfLifeDays\" > 0"));

        builder.HasData(CategorySeed.Categories);
    }
}
