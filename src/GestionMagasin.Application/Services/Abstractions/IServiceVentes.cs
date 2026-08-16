using GestionMagasin.Application.DTOs;
using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>Enregistrement et consultation des ventes de la caisse.</summary>
public interface IServiceVentes
{
    /// <summary>
    /// Enregistre une vente complète dans une seule transaction : contrôle du
    /// stock, création du ticket et de ses lignes, déduction des quantités,
    /// mouvements de stock et paiements. Si la moindre étape échoue, rien
    /// n'est écrit : il ne peut jamais exister de vente sans mouvement de
    /// stock correspondant.
    /// </summary>
    Task<VenteDto> EnregistrerVenteAsync(DemandeVente demande, CancellationToken jeton = default);

    /// <summary>
    /// Annule une vente et réintègre l'intégralité des articles au stock.
    /// Refusée si un retour a déjà été enregistré sur cette vente.
    /// </summary>
    Task<VenteDto> AnnulerVenteAsync(int venteId, string motif, CancellationToken jeton = default);

    /// <summary>Ajoute un règlement à une vente partiellement payée.</summary>
    Task<VenteDto> AjouterPaiementAsync(
        int venteId,
        PaiementDemande paiement,
        CancellationToken jeton = default);

    /// <summary>Charge une vente avec ses lignes et ses règlements.</summary>
    Task<VenteDto?> ObtenirAsync(int venteId, CancellationToken jeton = default);

    /// <summary>Charge une vente à partir de son numéro imprimé sur le ticket.</summary>
    Task<VenteDto?> ObtenirParNumeroAsync(string numeroVente, CancellationToken jeton = default);

    /// <summary>Liste les ventes selon les filtres de l'écran « Ventes ».</summary>
    Task<IReadOnlyList<ResumeVenteDto>> RechercherAsync(
        DateTime? debut = null,
        DateTime? fin = null,
        int? clientId = null,
        int? utilisateurId = null,
        StatutVente? statut = null,
        string? recherche = null,
        int limite = 500,
        CancellationToken jeton = default);
}
