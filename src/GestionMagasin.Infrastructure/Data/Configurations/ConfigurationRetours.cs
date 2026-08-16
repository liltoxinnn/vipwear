using GestionMagasin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionMagasin.Infrastructure.Data.Configurations;

public class ConfigurationRetour : IEntityTypeConfiguration<Retour>
{
    public void Configure(EntityTypeBuilder<Retour> constructeur)
    {
        constructeur.ToTable("retours");

        constructeur.HasKey(r => r.Id);

        constructeur.Property(r => r.NumeroRetour).IsRequired().HasMaxLength(30);
        constructeur.Property(r => r.Motif).HasMaxLength(1000);
        constructeur.Property(r => r.Statut).IsRequired().HasConversion<int>();
        constructeur.Property(r => r.DateRetour).HasColumnType("timestamp with time zone");

        constructeur.HasIndex(r => r.NumeroRetour).IsUnique();
        constructeur.HasIndex(r => r.DateRetour);
        constructeur.HasIndex(r => r.VenteId);
        constructeur.HasIndex(r => r.ClientId);

        // Le retour reste rattaché à sa vente d'origine, qui n'est jamais supprimée.
        constructeur.HasOne(r => r.Vente)
            .WithMany(v => v.Retours)
            .HasForeignKey(r => r.VenteId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.HasOne(r => r.Client)
            .WithMany()
            .HasForeignKey(r => r.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.HasOne(r => r.Utilisateur)
            .WithMany()
            .HasForeignKey(r => r.UtilisateurId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.ToTable(t =>
            t.HasCheckConstraint("ck_retours_montant_positif", "total_rembourse >= 0"));
    }
}

public class ConfigurationLigneRetour : IEntityTypeConfiguration<LigneRetour>
{
    public void Configure(EntityTypeBuilder<LigneRetour> constructeur)
    {
        constructeur.ToTable("lignes_retour");

        constructeur.HasKey(l => l.Id);

        constructeur.Property(l => l.EtatArticle).IsRequired().HasConversion<int>();
        constructeur.Property(l => l.RemisEnStock).HasDefaultValue(false);

        constructeur.HasIndex(l => l.RetourId);
        constructeur.HasIndex(l => l.LigneVenteId);
        constructeur.HasIndex(l => l.VarianteProduitId);

        constructeur.HasOne(l => l.Retour)
            .WithMany(r => r.Lignes)
            .HasForeignKey(l => l.RetourId)
            .OnDelete(DeleteBehavior.Cascade);

        constructeur.HasOne(l => l.LigneVente)
            .WithMany()
            .HasForeignKey(l => l.LigneVenteId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.HasOne(l => l.VarianteProduit)
            .WithMany()
            .HasForeignKey(l => l.VarianteProduitId)
            .OnDelete(DeleteBehavior.Restrict);

        constructeur.ToTable(t =>
        {
            t.HasCheckConstraint("ck_lignes_retour_quantite_positive", "quantite > 0");
            t.HasCheckConstraint("ck_lignes_retour_montant_positif", "montant_rembourse >= 0");

            // Un article endommagé ne peut jamais avoir été remis en stock :
            // la base refuse cette combinaison incohérente.
            t.HasCheckConstraint("ck_lignes_retour_etat_coherent",
                "remis_en_stock = false OR etat_article = 1");
        });
    }
}
