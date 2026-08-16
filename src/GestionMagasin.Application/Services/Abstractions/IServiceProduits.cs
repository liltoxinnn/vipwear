using GestionMagasin.Application.DTOs;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>Gestion du catalogue : produits, déclinaisons et données de référence.</summary>
public interface IServiceProduits
{
    // --- Produits ---

    Task<ProduitDto> CreerProduitAsync(DemandeProduit demande, CancellationToken jeton = default);

    Task<ProduitDto> ModifierProduitAsync(int produitId, DemandeProduit demande, CancellationToken jeton = default);

    /// <summary>
    /// Active ou désactive un produit. Un produit désactivé disparaît de la
    /// caisse mais reste présent dans tout l'historique des ventes.
    /// </summary>
    Task<ProduitDto> DefinirEtatProduitAsync(int produitId, bool actif, CancellationToken jeton = default);

    Task<ProduitDto?> ObtenirProduitAsync(int produitId, CancellationToken jeton = default);

    Task<IReadOnlyList<ResumeProduitDto>> RechercherProduitsAsync(
        string? recherche = null,
        int? marqueId = null,
        string? collection = null,
        string? saison = null,
        bool inclureInactifs = false,
        int limite = 500,
        CancellationToken jeton = default);

    // --- Déclinaisons ---

    Task<VarianteDto> AjouterVarianteAsync(
        int produitId,
        DemandeVariante demande,
        CancellationToken jeton = default);

    Task<VarianteDto> ModifierVarianteAsync(
        int varianteId,
        DemandeVariante demande,
        CancellationToken jeton = default);

    Task<VarianteDto> DefinirEtatVarianteAsync(int varianteId, bool actif, CancellationToken jeton = default);

    /// <summary>
    /// Crée en une fois toutes les combinaisons de tailles et de couleurs
    /// choisies. Les combinaisons déjà existantes sont ignorées, jamais
    /// dupliquées.
    /// </summary>
    Task<int> GenererVariantesAsync(
        int produitId,
        IReadOnlyList<int> tailleIds,
        IReadOnlyList<int> couleurIds,
        int seuilMinimum = 0,
        CancellationToken jeton = default);

    /// <summary>Recherche une déclinaison par code-barres, pour la caisse.</summary>
    Task<VarianteDto?> ObtenirVarianteParCodeBarresAsync(string codeBarres, CancellationToken jeton = default);

    /// <summary>Recherche libre en caisse : code-barres, code article, référence ou nom.</summary>
    Task<IReadOnlyList<VarianteDto>> RechercherVariantesAsync(
        string terme,
        int limite = 50,
        CancellationToken jeton = default);

    // --- Données de référence ---

    Task<IReadOnlyList<ReferenceDto>> ListerMarquesAsync(bool inclureInactifs = false, CancellationToken jeton = default);

    Task<IReadOnlyList<ReferenceDto>> ListerTaillesAsync(bool inclureInactifs = false, CancellationToken jeton = default);

    Task<IReadOnlyList<ReferenceDto>> ListerCouleursAsync(bool inclureInactifs = false, CancellationToken jeton = default);

    Task<ReferenceDto> EnregistrerMarqueAsync(int? id, string nom, string? description, CancellationToken jeton = default);

    Task<ReferenceDto> EnregistrerTailleAsync(int? id, string nom, int ordre, CancellationToken jeton = default);

    Task<ReferenceDto> EnregistrerCouleurAsync(int? id, string nom, string? codeCouleur, CancellationToken jeton = default);

    Task DefinirEtatMarqueAsync(int id, bool actif, CancellationToken jeton = default);

    Task DefinirEtatTailleAsync(int id, bool actif, CancellationToken jeton = default);

    Task DefinirEtatCouleurAsync(int id, bool actif, CancellationToken jeton = default);

    /// <summary>Listes des collections et saisons déjà saisies, pour l'auto-complétion.</summary>
    Task<IReadOnlyList<string>> ListerCollectionsAsync(CancellationToken jeton = default);

    Task<IReadOnlyList<string>> ListerSaisonsAsync(CancellationToken jeton = default);
}
