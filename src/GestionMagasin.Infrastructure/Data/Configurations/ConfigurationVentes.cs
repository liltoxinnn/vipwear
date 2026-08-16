using GestionMagasin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionMagasin.Infrastructure.Data.Configurations;

public class ConfigurationClient : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> constructeur)
    {
        constructeur.ToTable("clients");

        constructeur.HasKey(c => c.Id);

        constructeur.Property(c => c.Nom).IsRequired().HasMaxLength(100);
        constructeur.Property(c => c.Prenom).HasMaxLength(100);
        constructeur.Property(c => c.Telephone).HasMaxLength(30);
        constructeur.Property(c => c.Email).HasMaxLength(150);
        constructeur.Property(c => c.Adresse).HasMaxLength(400);
        constructeur.Property(c => c.Notes).HasMaxLength(2000);
        constructeur.Property(c => c.Actif).HasDefaultValue(true);
        constructeur.Property(c => c.DateCreation).HasColumnType("timestamp with time zone");

        constructeur.Ignore(c => c.NomComplet);

        constructeur.HasIndex(c => c.Nom);
        constructeur.HasIndex(c => c.Telephone);
        constructeur.HasIndex(c => c.Actif);
    }
}

public class ConfigurationVente : IEntityTypeConfiguration<Vente>
{
    public void Configure(EntityTypeBuilder<Vente> constructeur)
    {
        constructeur.ToTable("ventes");

        constructeur.HasKey(v => v.Id);

        constructeur.Property(v => v.NumeroVente).IsRequired().HasMaxLength(30);
        constructeur.Property(v => v.Statut).IsRequired().HasConversion<int>();
        constructeur.Property(v => v.Notes).HasMaxLength(2000);
        constructeur.Property(v => v.DateVente).HasColumnType("timestamp with time zone");

        constructeur.Ignore(v => v.MontantRestant);

        constructeur.HasIndex(v => v.NumeroVente).IsUnique();
        constructeur.HasIndex(v => v.DateVente);
        constructeur.HasIndex(v => v.ClientId);
        constructeur.HasIndex(v => v.UtilisateurId);
        constructeur.HasIndex(v => v.Statut);

        // Un client désactivé conserve ses ventes : suppression interdite.
        constructeur.HasOne(v => v.Client)
            .WithMany(c => c.Ventes)
            .HasForeignKey(v => v.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.HasOne(v => v.Utilisateur)
            .WithMany()
            .HasForeignKey(v => v.UtilisateurId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.ToTable(t =>
        {
            t.HasCheckConstraint("ck_ventes_montants_positifs",
                "sous_total >= 0 AND remise >= 0 AND total >= 0 AND montant_paye >= 0");
        });
    }
}

public class ConfigurationLigneVente : IEntityTypeConfiguration<LigneVente>
{
    public void Configure(EntityTypeBuilder<LigneVente> constructeur)
    {
        constructeur.ToTable("lignes_vente");

        constructeur.HasKey(l => l.Id);

        constructeur.Property(l => l.QuantiteRetournee).HasDefaultValue(0);

        constructeur.Ignore(l => l.QuantiteRetournable);

        constructeur.HasIndex(l => l.VenteId);
        constructeur.HasIndex(l => l.VarianteProduitId);

        constructeur.HasOne(l => l.Vente)
            .WithMany(v => v.Lignes)
            .HasForeignKey(l => l.VenteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Une déclinaison vendue ne peut plus être supprimée du catalogue :
        // l'historique de vente doit rester complet et lisible.
        constructeur.HasOne(l => l.VarianteProduit)
            .WithMany()
            .HasForeignKey(l => l.VarianteProduitId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.ToTable(t =>
        {
            t.HasCheckConstraint("ck_lignes_vente_quantite_positive", "quantite > 0");
            t.HasCheckConstraint("ck_lignes_vente_retour_coherent",
                "quantite_retournee >= 0 AND quantite_retournee <= quantite");
            t.HasCheckConstraint("ck_lignes_vente_prix_positif", "prix_unitaire >= 0 AND remise >= 0");
        });
    }
}

public class ConfigurationPaiement : IEntityTypeConfiguration<Paiement>
{
    public void Configure(EntityTypeBuilder<Paiement> constructeur)
    {
        constructeur.ToTable("paiements");

        constructeur.HasKey(p => p.Id);

        constructeur.Property(p => p.ModePaiement).IsRequired().HasConversion<int>();
        constructeur.Property(p => p.Reference).HasMaxLength(100);
        constructeur.Property(p => p.DatePaiement).HasColumnType("timestamp with time zone");

        constructeur.HasIndex(p => p.VenteId);
        constructeur.HasIndex(p => p.DatePaiement);
        constructeur.HasIndex(p => p.ModePaiement);

        constructeur.HasOne(p => p.Vente)
            .WithMany(v => v.Paiements)
            .HasForeignKey(p => p.VenteId)
            .OnDelete(DeleteBehavior.Cascade);

        constructeur.ToTable(t =>
            t.HasCheckConstraint("ck_paiements_montant_positif", "montant > 0"));
    }
}
