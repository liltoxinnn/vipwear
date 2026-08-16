using GestionMagasin.Application.DTOs;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>
/// Statistiques et rapports. Toutes les valeurs sont calculées à partir des
/// écritures réelles : ventes, lignes de vente, retours, achats et stock.
/// Aucun chiffre n'est estimé ni mis en cache.
/// </summary>
public interface IServiceRapports
{
    /// <summary>Indicateurs de synthèse sur une période.</summary>
    Task<SyntheseActiviteDto> ObtenirSyntheseAsync(Periode periode, CancellationToken jeton = default);

    /// <summary>Chiffre d'affaires jour par jour.</summary>
    Task<IReadOnlyList<PointStatistique>> ObtenirVentesParJourAsync(
        Periode periode,
        CancellationToken jeton = default);

    /// <summary>Chiffre d'affaires mois par mois.</summary>
    Task<IReadOnlyList<PointStatistique>> ObtenirVentesParMoisAsync(
        Periode periode,
        CancellationToken jeton = default);

    /// <summary>Articles les plus vendus sur la période.</summary>
    Task<IReadOnlyList<ClassementArticleDto>> ObtenirMeilleuresVentesAsync(
        Periode periode,
        int limite = 20,
        CancellationToken jeton = default);

    /// <summary>
    /// Articles les moins vendus, déclinaisons jamais vendues comprises :
    /// c'est cette liste qui aide à repérer le stock dormant.
    /// </summary>
    Task<IReadOnlyList<ClassementArticleDto>> ObtenirMoinsBonnesVentesAsync(
        Periode periode,
        int limite = 20,
        CancellationToken jeton = default);

    Task<IReadOnlyList<VentesParEmployeDto>> ObtenirVentesParEmployeAsync(
        Periode periode,
        CancellationToken jeton = default);

    Task<IReadOnlyList<VentesParModePaiementDto>> ObtenirVentesParModePaiementAsync(
        Periode periode,
        CancellationToken jeton = default);

    Task<SyntheseAchatsDto> ObtenirSyntheseAchatsAsync(Periode periode, CancellationToken jeton = default);

    Task<SyntheseRetoursDto> ObtenirSyntheseRetoursAsync(Periode periode, CancellationToken jeton = default);

    /// <summary>État et valorisation du stock à l'instant présent.</summary>
    Task<SyntheseStockDto> ObtenirSyntheseStockAsync(CancellationToken jeton = default);

    /// <summary>Toutes les données du tableau de bord, en une seule opération.</summary>
    Task<TableauBordDto> ObtenirTableauBordAsync(CancellationToken jeton = default);
}
