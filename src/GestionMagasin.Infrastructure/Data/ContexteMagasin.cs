using GestionMagasin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Infrastructure.Data;

/// <summary>
/// Contexte Entity Framework Core du magasin. Il décrit l'intégralité du
/// modèle relationnel PostgreSQL : tables, relations, index et contraintes.
/// </summary>
public class ContexteMagasin : DbContext
{
    public ContexteMagasin(DbContextOptions<ContexteMagasin> options) : base(options)
    {
    }

    // --- Sécurité ---
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // --- Catalogue ---
    public DbSet<Marque> Marques => Set<Marque>();
    public DbSet<SystemeTaille> SystemesTailles => Set<SystemeTaille>();
    public DbSet<Categorie> Categories => Set<Categorie>();
    public DbSet<Taille> Tailles => Set<Taille>();
    public DbSet<Couleur> Couleurs => Set<Couleur>();
    public DbSet<Produit> Produits => Set<Produit>();
    public DbSet<VarianteProduit> VariantesProduits => Set<VarianteProduit>();

    // --- Stock ---
    public DbSet<Inventaire> Inventaires => Set<Inventaire>();
    public DbSet<MouvementStock> MouvementsStock => Set<MouvementStock>();

    // --- Achats ---
    public DbSet<Fournisseur> Fournisseurs => Set<Fournisseur>();
    public DbSet<Achat> Achats => Set<Achat>();
    public DbSet<LigneAchat> LignesAchat => Set<LigneAchat>();

    // --- Ventes ---
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Vente> Ventes => Set<Vente>();
    public DbSet<LigneVente> LignesVente => Set<LigneVente>();
    public DbSet<Paiement> Paiements => Set<Paiement>();

    // --- Retours ---
    public DbSet<Retour> Retours => Set<Retour>();
    public DbSet<LigneRetour> LignesRetour => Set<LigneRetour>();

    // --- Exploitation ---
    public DbSet<ParametresMagasin> ParametresMagasin => Set<ParametresMagasin>();
    public DbSet<JournalAudit> JournauxAudit => Set<JournalAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContexteMagasin).Assembly);

        // Séquences alimentant les numéros de documents lisibles (VTE-…, ACH-…, RET-…).
        // Une séquence PostgreSQL garantit l'unicité même avec plusieurs caisses.
        modelBuilder.HasSequence<long>(SequencesDocuments.Ventes).StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>(SequencesDocuments.Achats).StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>(SequencesDocuments.Retours).StartsAt(1).IncrementsBy(1);

        ConventionsNommage.AppliquerSnakeCase(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Tous les montants sont des décimaux exacts : jamais de virgule
        // flottante pour de la monnaie.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
