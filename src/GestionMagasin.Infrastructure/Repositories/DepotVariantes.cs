using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Infrastructure.Repositories;

/// <summary>Accès aux déclinaisons de produits, optimisé pour la caisse.</summary>
public class DepotVariantes : DepotGenerique<VarianteProduit>, IDepotVariantes
{
    public DepotVariantes(ContexteMagasin contexte) : base(contexte)
    {
    }

    public Task<VarianteProduit?> ObtenirParCodeBarresAsync(string codeBarres, CancellationToken jeton = default)
    {
        var recherche = codeBarres.Trim();

        return Ensemble.AsNoTracking()
            .Include(v => v.Produit).ThenInclude(p => p!.Marque)
            .Include(v => v.Taille)
            .Include(v => v.Couleur)
            .Include(v => v.Inventaire)
            .FirstOrDefaultAsync(v => v.CodeBarres == recherche, jeton);
    }

    public Task<VarianteProduit?> ObtenirParSkuAsync(string sku, CancellationToken jeton = default)
    {
        var recherche = sku.Trim();

        return Ensemble.AsNoTracking()
            .Include(v => v.Produit).ThenInclude(p => p!.Marque)
            .Include(v => v.Taille)
            .Include(v => v.Couleur)
            .Include(v => v.Inventaire)
            .FirstOrDefaultAsync(v => v.SKU == recherche, jeton);
    }

    public Task<VarianteProduit?> ObtenirCompleteAsync(int id, CancellationToken jeton = default) =>
        Ensemble.AsNoTracking()
            .Include(v => v.Produit).ThenInclude(p => p!.Marque)
            .Include(v => v.Taille)
            .Include(v => v.Couleur)
            .Include(v => v.Inventaire)
            .FirstOrDefaultAsync(v => v.Id == id, jeton);

    public Task<bool> CombinaisonExisteAsync(
        int produitId,
        int tailleId,
        int couleurId,
        int? idAExclure = null,
        CancellationToken jeton = default) =>
        Ensemble.AsNoTracking().AnyAsync(
            v => v.ProduitId == produitId
                 && v.TailleId == tailleId
                 && v.CouleurId == couleurId
                 && (idAExclure == null || v.Id != idAExclure),
            jeton);

    public Task<bool> CodeBarresExisteAsync(string codeBarres, int? idAExclure = null, CancellationToken jeton = default)
    {
        var recherche = codeBarres.Trim();

        return Ensemble.AsNoTracking().AnyAsync(
            v => v.CodeBarres == recherche && (idAExclure == null || v.Id != idAExclure),
            jeton);
    }

    public Task<bool> SkuExisteAsync(string sku, int? idAExclure = null, CancellationToken jeton = default)
    {
        var recherche = sku.Trim();

        return Ensemble.AsNoTracking().AnyAsync(
            v => v.SKU == recherche && (idAExclure == null || v.Id != idAExclure),
            jeton);
    }

    public async Task<IReadOnlyList<VarianteProduit>> RechercherAsync(
        string terme,
        int limite = 50,
        CancellationToken jeton = default)
    {
        var recherche = terme.Trim();

        if (recherche.Length == 0)
        {
            return Array.Empty<VarianteProduit>();
        }

        var motif = $"%{recherche}%";

        return await Ensemble.AsNoTracking()
            .Include(v => v.Produit).ThenInclude(p => p!.Marque)
            .Include(v => v.Taille)
            .Include(v => v.Couleur)
            .Include(v => v.Inventaire)
            .Where(v => v.Actif && v.Produit.Actif)
            .Where(v => v.CodeBarres == recherche
                        || EF.Functions.ILike(v.SKU, motif)
                        || EF.Functions.ILike(v.Produit.Nom, motif)
                        || EF.Functions.ILike(v.Produit.Reference, motif))
            // Une correspondance exacte de code-barres remonte toujours en tête.
            .OrderByDescending(v => v.CodeBarres == recherche)
            .ThenBy(v => v.Produit.Nom)
            .ThenBy(v => v.Couleur.Nom)
            .ThenBy(v => v.Taille.Ordre)
            .Take(limite)
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }
}
