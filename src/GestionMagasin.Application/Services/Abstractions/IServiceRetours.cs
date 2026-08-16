using GestionMagasin.Application.DTOs;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>Reprises d'articles et échanges.</summary>
public interface IServiceRetours
{
    /// <summary>
    /// Enregistre un retour dans une seule transaction : contrôle des
    /// quantités reprenables, réintégration au stock des seuls articles
    /// revendables, mise à jour de la vente d'origine et calcul du
    /// remboursement.
    /// </summary>
    Task<RetourDto> EnregistrerRetourAsync(DemandeRetour demande, CancellationToken jeton = default);

    /// <summary>
    /// Enregistre un échange : le retour des articles rapportés et la vente
    /// des articles emportés sont validés ensemble ou pas du tout.
    /// </summary>
    Task<ResultatEchangeDto> EnregistrerEchangeAsync(DemandeEchange demande, CancellationToken jeton = default);

    Task<RetourDto?> ObtenirAsync(int retourId, CancellationToken jeton = default);

    Task<IReadOnlyList<ResumeRetourDto>> RechercherAsync(
        DateTime? debut = null,
        DateTime? fin = null,
        int? clientId = null,
        string? recherche = null,
        int limite = 500,
        CancellationToken jeton = default);
}
