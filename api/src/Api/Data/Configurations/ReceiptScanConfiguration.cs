using Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public class ReceiptScanConfiguration : IEntityTypeConfiguration<ReceiptScan>
{
    public void Configure(EntityTypeBuilder<ReceiptScan> builder)
    {
        // Compte supprimé : ses lectures disparaissent avec lui (elles ne servent qu'aux quotas).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comptage des quotas du jour : par utilisateur, et global par date.
        builder.HasIndex(r => new { r.UserId, r.CreatedAt });
        builder.HasIndex(r => r.CreatedAt);
    }
}
