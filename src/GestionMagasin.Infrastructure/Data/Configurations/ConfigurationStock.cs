using GestionMagasin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionMagasin.Infrastructure.Data.Configurations;

public class ConfigurationInventaire : IEntityTypeConfiguration<Inventaire>
{
    public void Configure(EntityTypeBuilder<Inventaire> constructeur)
    {
        constructeur.ToTable("inventaires");

        constructeur.HasKey(i => i.Id);

        constructeur.Property(i => i.QuantiteDisponible).HasDefaultValue(0);
        constructeur.Property(i => i.DateModification).HasColumnType("timestamp with time zone");

        // Jeton de concurrence natif de PostgreSQL : la colonne système xmin
        // change à chaque écriture de la ligne. Si deux postes modifient la
        // même quantité, le second est rejeté au lieu d'écraser le premier.
        constructeur.Property(i => i.VersionEnregistrement)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Une seule ligne de stock par déclinaison.
        constructeur.HasIndex(i => i.VarianteProduitId)
            .IsUnique()
            .HasDatabaseName("ux_inventaires_variante");

        constructeur.HasOne(i => i.VarianteProduit)
            .WithOne(v => v.Inventaire)
            .HasForeignKey<Inventaire>(i => i.VarianteProduitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dernier rempart contre un stock négatif : même une écriture SQL
        // directe est refusée par la base.
        constructeur.ToTable(t =>
            t.HasCheckConstraint("ck_inventaires_quantite_positive", "quantite_disponible >= 0"));
    }
}

public class ConfigurationMouvementStock : IEntityTypeConfiguration<MouvementStock>
{
    public void Configure(EntityTypeBuilder<MouvementStock> constructeur)
    {
        constructeur.ToTable("mouvements_stock");

        constructeur.HasKey(m => m.Id);

        constructeur.Property(m => m.TypeMouvement).IsRequired().HasConversion<int>();
        constructeur.Property(m => m.Motif).HasMaxLength(400);
        constructeur.Property(m => m.ReferenceDocument).HasMaxLength(60);
        constructeur.Property(m => m.DateCreation).HasColumnType("timestamp with time zone");

        // L'historique d'une déclinaison se lit du plus récent au plus ancien.
        constructeur.HasIndex(m => new { m.VarianteProduitId, m.DateCreation });
        constructeur.HasIndex(m => m.DateCreation);
        constructeur.HasIndex(m => m.ReferenceDocument);
        constructeur.HasIndex(m => m.TypeMouvement);

        constructeur.HasOne(m => m.VarianteProduit)
            .WithMany(v => v.MouvementsStock)
            .HasForeignKey(m => m.VarianteProduitId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.HasOne(m => m.Utilisateur)
            .WithMany()
            .HasForeignKey(m => m.UtilisateurId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
