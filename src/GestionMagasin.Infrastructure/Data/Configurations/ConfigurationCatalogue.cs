using GestionMagasin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionMagasin.Infrastructure.Data.Configurations;

public class ConfigurationMarque : IEntityTypeConfiguration<Marque>
{
    public void Configure(EntityTypeBuilder<Marque> constructeur)
    {
        constructeur.ToTable("marques");

        constructeur.HasKey(m => m.Id);

        constructeur.Property(m => m.Nom).IsRequired().HasMaxLength(100);
        constructeur.Property(m => m.Description).HasMaxLength(300);
        constructeur.Property(m => m.Actif).HasDefaultValue(true);

        constructeur.HasIndex(m => m.Nom).IsUnique();
    }
}

public class ConfigurationSystemeTaille : IEntityTypeConfiguration<SystemeTaille>
{
    public void Configure(EntityTypeBuilder<SystemeTaille> constructeur)
    {
        constructeur.ToTable("systemes_tailles");

        constructeur.HasKey(s => s.Id);

        constructeur.Property(s => s.Nom).IsRequired().HasMaxLength(80);
        constructeur.Property(s => s.Actif).HasDefaultValue(true);

        constructeur.HasIndex(s => s.Nom).IsUnique();
        constructeur.HasIndex(s => s.Ordre);
    }
}

public class ConfigurationCategorie : IEntityTypeConfiguration<Categorie>
{
    public void Configure(EntityTypeBuilder<Categorie> constructeur)
    {
        constructeur.ToTable("categories");

        constructeur.HasKey(c => c.Id);

        constructeur.Property(c => c.Nom).IsRequired().HasMaxLength(80);
        constructeur.Property(c => c.Actif).HasDefaultValue(true);

        constructeur.HasIndex(c => c.Nom).IsUnique();
        constructeur.HasIndex(c => c.Ordre);

        // Une catégorie sans système de tailles ne saurait plus quoi proposer
        // à la création d'une déclinaison : le lien est obligatoire, et le
        // système ne peut pas être supprimé tant qu'une famille s'en sert.
        constructeur.HasOne(c => c.SystemeTaille)
            .WithMany(s => s.Categories)
            .HasForeignKey(c => c.SystemeTailleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurationTaille : IEntityTypeConfiguration<Taille>
{
    public void Configure(EntityTypeBuilder<Taille> constructeur)
    {
        constructeur.ToTable("tailles");

        constructeur.HasKey(t => t.Id);

        constructeur.Property(t => t.Nom).IsRequired().HasMaxLength(20);
        constructeur.Property(t => t.Actif).HasDefaultValue(true);

        // « 42 » existe en pointure et en taille de pantalon : le nom n'est
        // unique qu'à l'intérieur de son système.
        constructeur.HasIndex(t => new { t.SystemeTailleId, t.Nom }).IsUnique();
        constructeur.HasIndex(t => t.Ordre);

        constructeur.HasOne(t => t.SystemeTaille)
            .WithMany(s => s.Tailles)
            .HasForeignKey(t => t.SystemeTailleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurationCouleur : IEntityTypeConfiguration<Couleur>
{
    public void Configure(EntityTypeBuilder<Couleur> constructeur)
    {
        constructeur.ToTable("couleurs");

        constructeur.HasKey(c => c.Id);

        constructeur.Property(c => c.Nom).IsRequired().HasMaxLength(50);
        constructeur.Property(c => c.CodeCouleur).HasMaxLength(9);
        constructeur.Property(c => c.Actif).HasDefaultValue(true);

        constructeur.HasIndex(c => c.Nom).IsUnique();
    }
}

public class ConfigurationProduit : IEntityTypeConfiguration<Produit>
{
    public void Configure(EntityTypeBuilder<Produit> constructeur)
    {
        constructeur.ToTable("produits");

        constructeur.HasKey(p => p.Id);

        constructeur.Property(p => p.Reference).IsRequired().HasMaxLength(60);
        constructeur.Property(p => p.SKU).IsRequired().HasMaxLength(60);
        constructeur.Property(p => p.Nom).IsRequired().HasMaxLength(200);
        constructeur.Property(p => p.Description).HasMaxLength(2000);
        constructeur.Property(p => p.Collection).HasMaxLength(100);
        constructeur.Property(p => p.Saison).HasMaxLength(60);
        constructeur.Property(p => p.Photo).HasMaxLength(400);
        constructeur.Property(p => p.Actif).HasDefaultValue(true);
        constructeur.Property(p => p.DateCreation).HasColumnType("timestamp with time zone");
        constructeur.Property(p => p.DateModification).HasColumnType("timestamp with time zone");

        // Champs les plus sollicités par la recherche produit.
        constructeur.HasIndex(p => p.Reference).IsUnique();
        constructeur.HasIndex(p => p.SKU).IsUnique();
        constructeur.HasIndex(p => p.Nom);
        constructeur.HasIndex(p => p.Actif);

        constructeur.HasIndex(p => p.CategorieId);

        constructeur.HasOne(p => p.Marque)
            .WithMany(m => m.Produits)
            .HasForeignKey(p => p.MarqueId)
            .OnDelete(DeleteBehavior.SetNull);

        // La famille commande le système de tailles : un produit sans famille
        // ne saurait pas quelles tailles proposer. Elle est donc obligatoire,
        // et ne peut pas être supprimée tant qu'elle porte des articles.
        constructeur.HasOne(p => p.Categorie)
            .WithMany(c => c.Produits)
            .HasForeignKey(p => p.CategorieId)
            .OnDelete(DeleteBehavior.Restrict);

        // Aucun prix ne peut être négatif, quelle que soit la voie d'écriture.
        constructeur.ToTable(t =>
        {
            t.HasCheckConstraint("ck_produits_prix_achat_positif", "prix_achat >= 0");
            t.HasCheckConstraint("ck_produits_prix_vente_positif", "prix_vente >= 0");
        });
    }
}

public class ConfigurationVarianteProduit : IEntityTypeConfiguration<VarianteProduit>
{
    public void Configure(EntityTypeBuilder<VarianteProduit> constructeur)
    {
        constructeur.ToTable("variantes_produits");

        constructeur.HasKey(v => v.Id);

        constructeur.Property(v => v.SKU).IsRequired().HasMaxLength(80);
        constructeur.Property(v => v.CodeBarres).HasMaxLength(80);
        constructeur.Property(v => v.SeuilMinimum).HasDefaultValue(0);
        constructeur.Property(v => v.Actif).HasDefaultValue(true);

        constructeur.Ignore(v => v.PrixAchatEffectif);
        constructeur.Ignore(v => v.PrixVenteEffectif);
        constructeur.Ignore(v => v.Designation);

        // Règle centrale du catalogue : une seule ligne par combinaison
        // produit + taille + couleur. La base refuse tout doublon, même si
        // deux postes tentent la création au même instant.
        constructeur.HasIndex(v => new { v.ProduitId, v.TailleId, v.CouleurId })
            .IsUnique()
            .HasDatabaseName("ux_variantes_produit_taille_couleur");

        constructeur.HasIndex(v => v.SKU).IsUnique();

        // Le code-barres est unique lorsqu'il est renseigné : plusieurs
        // variantes peuvent rester sans code-barres sans se gêner.
        constructeur.HasIndex(v => v.CodeBarres)
            .IsUnique()
            .HasFilter("code_barres IS NOT NULL")
            .HasDatabaseName("ux_variantes_code_barres");

        constructeur.HasOne(v => v.Produit)
            .WithMany(p => p.Variantes)
            .HasForeignKey(v => v.ProduitId)
            .OnDelete(DeleteBehavior.Cascade);

        constructeur.HasOne(v => v.Taille)
            .WithMany(t => t.Variantes)
            .HasForeignKey(v => v.TailleId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.HasOne(v => v.Couleur)
            .WithMany(c => c.Variantes)
            .HasForeignKey(v => v.CouleurId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.ToTable(t =>
        {
            t.HasCheckConstraint("ck_variantes_prix_achat_positif", "prix_achat IS NULL OR prix_achat >= 0");
            t.HasCheckConstraint("ck_variantes_prix_vente_positif", "prix_vente IS NULL OR prix_vente >= 0");
            t.HasCheckConstraint("ck_variantes_seuil_positif", "seuil_minimum >= 0");
        });
    }
}
