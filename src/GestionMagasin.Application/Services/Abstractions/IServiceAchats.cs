using GestionMagasin.Application.DTOs;
using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>Commandes fournisseurs et réception de la marchandise.</summary>
public interface IServiceAchats
{
    /// <summary>
    /// Enregistre une commande fournisseur. Le stock n'est pas modifié à ce
    /// stade : il ne le sera qu'à la réception effective de la marchandise.
    /// </summary>
    Task<AchatDto> CreerAchatAsync(DemandeAchat demande, CancellationToken jeton = default);

    /// <summary>
    /// Réceptionne tout ou partie d'une commande : le stock augmente des
    /// quantités reçues et un mouvement est créé pour chacune d'elles.
    /// </summary>
    Task<AchatDto> ReceptionnerAsync(
        int achatId,
        IReadOnlyList<LigneReception> receptions,
        CancellationToken jeton = default);

    /// <summary>Réceptionne d'un seul geste tout ce qui reste à recevoir.</summary>
    Task<AchatDto> ReceptionnerToutAsync(int achatId, CancellationToken jeton = default);

    /// <summary>Enregistre un règlement versé au fournisseur.</summary>
    Task<AchatDto> EnregistrerReglementAsync(
        int achatId,
        decimal montant,
        CancellationToken jeton = default);

    /// <summary>
    /// Annule une commande. Impossible dès lors qu'une partie de la
    /// marchandise a déjà rejoint le stock.
    /// </summary>
    Task<AchatDto> AnnulerAchatAsync(int achatId, string motif, CancellationToken jeton = default);

    Task<AchatDto?> ObtenirAsync(int achatId, CancellationToken jeton = default);

    Task<IReadOnlyList<ResumeAchatDto>> RechercherAsync(
        DateTime? debut = null,
        DateTime? fin = null,
        int? fournisseurId = null,
        StatutAchat? statut = null,
        string? recherche = null,
        int limite = 500,
        CancellationToken jeton = default);
}
