using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Infrastructure.Repositories;

/// <summary>
/// Accès aux quantités en stock. Les lectures destinées à une écriture
/// passent par un verrou PostgreSQL « FOR UPDATE » : tant que la transaction
/// n'est pas terminée, aucune autre caisse ne peut modifier la même ligne.
/// C'est ce qui rend impossible la vente simultanée du dernier article.
/// </summary>
public class DepotInventaire : DepotGenerique<Inventaire>, IDepotInventaire
{
    // xmin est une colonne système : elle doit être demandée explicitement
    // car « SELECT * » ne la renvoie pas, alors que le modèle l'attend.
    private const string ColonnesInventaire =
        "id, variante_produit_id, quantite_disponible, date_modification, xmin";

    public DepotInventaire(ContexteMagasin contexte) : base(contexte)
    {
    }

    public async Task<Inventaire?> ObtenirPourMiseAJourAsync(int varianteProduitId, CancellationToken jeton = default)
    {
        var lignes = await Ensemble
            .FromSql($"SELECT id, variante_produit_id, quantite_disponible, date_modification, xmin FROM inventaires WHERE variante_produit_id = {varianteProduitId} FOR UPDATE")
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return lignes.FirstOrDefault();
    }

    public async Task<Inventaire> ObtenirOuCreerPourMiseAJourAsync(int varianteProduitId, CancellationToken jeton = default)
    {
        var existante = await ObtenirPourMiseAJourAsync(varianteProduitId, jeton).ConfigureAwait(false);
        if (existante is not null)
        {
            return existante;
        }

        // La contrainte d'unicité sur variante_produit_id fait de cette
        // insertion une opération sûre même si deux transactions la tentent
        // en même temps : la seconde ne crée rien et relit la ligne existante.
        await Contexte.Database.ExecuteSqlAsync(
            $@"INSERT INTO inventaires (variante_produit_id, quantite_disponible, date_modification)
               VALUES ({varianteProduitId}, 0, NOW())
               ON CONFLICT (variante_produit_id) DO NOTHING",
            jeton).ConfigureAwait(false);

        var creee = await ObtenirPourMiseAJourAsync(varianteProduitId, jeton).ConfigureAwait(false);

        return creee ?? throw new InvalidOperationException(
            $"La ligne d'inventaire de la variante {varianteProduitId} n'a pas pu être créée.");
    }

    public Task<Inventaire?> ObtenirParVarianteAsync(int varianteProduitId, CancellationToken jeton = default) =>
        Ensemble.AsNoTracking().FirstOrDefaultAsync(i => i.VarianteProduitId == varianteProduitId, jeton);

    public async Task<int> ObtenirQuantiteAsync(int varianteProduitId, CancellationToken jeton = default) =>
        await Ensemble.AsNoTracking()
            .Where(i => i.VarianteProduitId == varianteProduitId)
            .Select(i => (int?)i.QuantiteDisponible)
            .FirstOrDefaultAsync(jeton)
            .ConfigureAwait(false) ?? 0;

    public async Task<IReadOnlyDictionary<int, int>> ObtenirQuantitesAsync(
        IEnumerable<int> varianteProduitIds,
        CancellationToken jeton = default)
    {
        var identifiants = varianteProduitIds.Distinct().ToArray();

        if (identifiants.Length == 0)
        {
            return new Dictionary<int, int>();
        }

        return await Ensemble.AsNoTracking()
            .Where(i => identifiants.Contains(i.VarianteProduitId))
            .ToDictionaryAsync(i => i.VarianteProduitId, i => i.QuantiteDisponible, jeton)
            .ConfigureAwait(false);
    }
}
