using GestionMagasin.Application.DTOs;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>Gestion des fiches clients.</summary>
public interface IServiceClients
{
    Task<ClientDto> CreerAsync(DemandeClient demande, CancellationToken jeton = default);

    Task<ClientDto> ModifierAsync(int clientId, DemandeClient demande, CancellationToken jeton = default);

    /// <summary>
    /// Active ou désactive un client. Ses ventes passées restent intactes :
    /// une fiche client n'est jamais supprimée physiquement.
    /// </summary>
    Task<ClientDto> DefinirEtatAsync(int clientId, bool actif, CancellationToken jeton = default);

    Task<ClientDto?> ObtenirAsync(int clientId, CancellationToken jeton = default);

    Task<IReadOnlyList<ClientDto>> RechercherAsync(
        string? recherche = null,
        bool inclureInactifs = false,
        int limite = 500,
        CancellationToken jeton = default);

    /// <summary>Historique des ventes d'un client.</summary>
    Task<IReadOnlyList<ResumeVenteDto>> ObtenirHistoriqueAsync(int clientId, CancellationToken jeton = default);
}

/// <summary>Gestion des fiches fournisseurs.</summary>
public interface IServiceFournisseurs
{
    Task<FournisseurDto> CreerAsync(DemandeFournisseur demande, CancellationToken jeton = default);

    Task<FournisseurDto> ModifierAsync(int fournisseurId, DemandeFournisseur demande, CancellationToken jeton = default);

    Task<FournisseurDto> DefinirEtatAsync(int fournisseurId, bool actif, CancellationToken jeton = default);

    Task<FournisseurDto?> ObtenirAsync(int fournisseurId, CancellationToken jeton = default);

    Task<IReadOnlyList<FournisseurDto>> RechercherAsync(
        string? recherche = null,
        bool inclureInactifs = false,
        int limite = 500,
        CancellationToken jeton = default);

    /// <summary>Historique des commandes passées à un fournisseur.</summary>
    Task<IReadOnlyList<ResumeAchatDto>> ObtenirHistoriqueAsync(int fournisseurId, CancellationToken jeton = default);
}
