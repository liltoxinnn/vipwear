using GestionMagasin.Application.DTOs;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>
/// Production des documents imprimables en français : tickets de caisse,
/// factures, bons de commande et rapports.
/// </summary>
public interface IServiceDocumentsPdf
{
    /// <summary>Ticket de caisse au format rouleau 80 mm.</summary>
    Task<byte[]> GenererTicketAsync(VenteDto vente, CancellationToken jeton = default);

    /// <summary>Facture A4 détaillée, avec les mentions légales du magasin.</summary>
    Task<byte[]> GenererFactureAsync(VenteDto vente, CancellationToken jeton = default);

    /// <summary>Bon de commande fournisseur au format A4.</summary>
    Task<byte[]> GenererBonCommandeAsync(AchatDto achat, CancellationToken jeton = default);

    /// <summary>Justificatif de retour remis au client.</summary>
    Task<byte[]> GenererBonRetourAsync(RetourDto retour, CancellationToken jeton = default);

    /// <summary>Rapport d'activité complet sur une période.</summary>
    Task<byte[]> GenererRapportActiviteAsync(
        Periode periode,
        SyntheseActiviteDto synthese,
        IReadOnlyList<ClassementArticleDto> meilleuresVentes,
        IReadOnlyList<VentesParEmployeDto> ventesParEmploye,
        IReadOnlyList<VentesParModePaiementDto> repartitionPaiements,
        CancellationToken jeton = default);

    /// <summary>État du stock imprimable, avec la valorisation.</summary>
    Task<byte[]> GenererRapportStockAsync(
        IReadOnlyList<LigneStockDto> lignes,
        SyntheseStockDto synthese,
        CancellationToken jeton = default);
}

/// <summary>Exports Excel exploitables dans un tableur.</summary>
public interface IServiceExportExcel
{
    Task<byte[]> ExporterStockAsync(IReadOnlyList<LigneStockDto> lignes, CancellationToken jeton = default);

    Task<byte[]> ExporterProduitsAsync(IReadOnlyList<ProduitDto> produits, CancellationToken jeton = default);

    Task<byte[]> ExporterVentesAsync(
        IReadOnlyList<ResumeVenteDto> ventes,
        Periode periode,
        CancellationToken jeton = default);

    Task<byte[]> ExporterAchatsAsync(
        IReadOnlyList<ResumeAchatDto> achats,
        Periode periode,
        CancellationToken jeton = default);

    Task<byte[]> ExporterRetoursAsync(
        IReadOnlyList<ResumeRetourDto> retours,
        Periode periode,
        CancellationToken jeton = default);

    Task<byte[]> ExporterMouvementsStockAsync(
        IReadOnlyList<MouvementStockDto> mouvements,
        CancellationToken jeton = default);

    /// <summary>Classeur de synthèse : indicateurs, classements et répartitions.</summary>
    Task<byte[]> ExporterRapportActiviteAsync(
        Periode periode,
        SyntheseActiviteDto synthese,
        IReadOnlyList<ClassementArticleDto> meilleuresVentes,
        IReadOnlyList<ClassementArticleDto> moinsBonnesVentes,
        IReadOnlyList<VentesParEmployeDto> ventesParEmploye,
        IReadOnlyList<VentesParModePaiementDto> repartitionPaiements,
        CancellationToken jeton = default);
}
