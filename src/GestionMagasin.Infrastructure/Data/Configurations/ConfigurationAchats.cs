using GestionMagasin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionMagasin.Infrastructure.Data.Configurations;

public class ConfigurationFournisseur : IEntityTypeConfiguration<Fournisseur>
{
    public void Configure(EntityTypeBuilder<Fournisseur> constructeur)
    {
        constructeur.ToTable("fournisseurs");

        constructeur.HasKey(f => f.Id);

        constructeur.Property(f => f.Nom).IsRequired().HasMaxLength(150);
        constructeur.Property(f => f.Entreprise).HasMaxLength(150);
        constructeur.Property(f => f.Telephone).HasMaxLength(30);
        constructeur.Property(f => f.Email).HasMaxLength(150);
        constructeur.Property(f => f.Adresse).HasMaxLength(400);
        constructeur.Property(f => f.Notes).HasMaxLength(2000);
        constructeur.Property(f => f.Actif).HasDefaultValue(true);
        constructeur.Property(f => f.DateCreation).HasColumnType("timestamp with time zone");

        constructeur.HasIndex(f => f.Nom);
        constructeur.HasIndex(f => f.Telephone);
        constructeur.HasIndex(f => f.Actif);
    }
}

public class ConfigurationAchat : IEntityTypeConfiguration<Achat>
{
    public void Configure(EntityTypeBuilder<Achat> constructeur)
    {
        constructeur.ToTable("achats");

        constructeur.HasKey(a => a.Id);

        constructeur.Property(a => a.NumeroAchat).IsRequired().HasMaxLength(30);
        constructeur.Property(a => a.Statut).IsRequired().HasConversion<int>();
        constructeur.Property(a => a.Notes).HasMaxLength(2000);
        constructeur.Property(a => a.DateAchat).HasColumnType("timestamp with time zone");
        constructeur.Property(a => a.DateReception).HasColumnType("timestamp with time zone");

        constructeur.HasIndex(a => a.NumeroAchat).IsUnique();
        constructeur.HasIndex(a => a.DateAchat);
        constructeur.HasIndex(a => a.FournisseurId);
        constructeur.HasIndex(a => a.Statut);

        // Un fournisseur ayant un historique d'achats ne peut pas être
        // supprimé : il est désactivé.
        constructeur.HasOne(a => a.Fournisseur)
            .WithMany(f => f.Achats)
            .HasForeignKey(a => a.FournisseurId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.HasOne(a => a.Utilisateur)
            .WithMany()
            .HasForeignKey(a => a.UtilisateurId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.ToTable(t =>
        {
            t.HasCheckConstraint("ck_achats_montants_positifs",
                "sous_total >= 0 AND remise >= 0 AND total >= 0 AND montant_paye >= 0");
        });
    }
}

public class ConfigurationLigneAchat : IEntityTypeConfiguration<LigneAchat>
{
    public void Configure(EntityTypeBuilder<LigneAchat> constructeur)
    {
        constructeur.ToTable("lignes_achat");

        constructeur.HasKey(l => l.Id);

        constructeur.Property(l => l.QuantiteRecue).HasDefaultValue(0);

        constructeur.Ignore(l => l.QuantiteRestante);

        constructeur.HasIndex(l => l.AchatId);
        constructeur.HasIndex(l => l.VarianteProduitId);

        // Les lignes appartiennent à la commande : elles disparaissent avec elle
        // tant qu'aucune réception n'a eu lieu.
        constructeur.HasOne(l => l.Achat)
            .WithMany(a => a.Lignes)
            .HasForeignKey(l => l.AchatId)
            .OnDelete(DeleteBehavior.Cascade);

        constructeur.HasOne(l => l.VarianteProduit)
            .WithMany()
            .HasForeignKey(l => l.VarianteProduitId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.ToTable(t =>
        {
            t.HasCheckConstraint("ck_lignes_achat_quantite_positive", "quantite > 0");
            t.HasCheckConstraint("ck_lignes_achat_recue_coherente",
                "quantite_recue >= 0 AND quantite_recue <= quantite");
            t.HasCheckConstraint("ck_lignes_achat_prix_positif", "prix_unitaire >= 0");
        });
    }
}
