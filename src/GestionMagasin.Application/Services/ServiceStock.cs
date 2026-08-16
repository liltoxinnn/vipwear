using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Domain.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.Application.Services;

/// <inheritdoc cref="IServiceStock"/>
public class ServiceStock : IServiceStock
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly ISessionUtilisateur _session;
    private readonly IFournisseurHorodatage _horodatage;
    private readonly ILogger<ServiceStock> _journal;

    public ServiceStock(
        IUniteDeTravail uniteDeTravail,
        ISessionUtilisateur session,
        IFournisseurHorodatage horodatage,
        ILogger<ServiceStock> journal)
    {
        _uniteDeTravail = uniteDeTravail;
        _session = session;
        _horodatage = horodatage;
        _journal = journal;
    }

    // ------------------------------------------------------------------
    // Opérations d'écriture
    // ------------------------------------------------------------------

    public Task<int> AjouterStockAsync(
        int varianteProduitId,
        int quantite,
        TypeMouvementStock typeMouvement,
        string? motif = null,
        string? referenceDocument = null,
        CancellationToken jeton = default) =>
        AppliquerVariationAsync(varianteProduitId, quantite, typeMouvement, motif, referenceDocument, jeton);

    public Task<int> RetirerStockAsync(
        int varianteProduitId,
        int quantite,
        TypeMouvementStock typeMouvement,
        string? motif = null,
        string? referenceDocument = null,
        CancellationToken jeton = default) =>
        AppliquerVariationAsync(varianteProduitId, -quantite, typeMouvement, motif, referenceDocument, jeton);

    public Task<int> RetournerStockAsync(
        int varianteProduitId,
        int quantite,
        string? motif = null,
        string? referenceDocument = null,
        CancellationToken jeton = default) =>
        AppliquerVariationAsync(
            varianteProduitId,
            quantite,
            TypeMouvementStock.RetourClient,
            motif ?? "Retour d'un article revendable",
            referenceDocument,
            jeton);

    public async Task<int> AjusterStockAsync(
        int varianteProduitId,
        int quantiteConstatee,
        string motif,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierStock);

        if (quantiteConstatee < 0)
        {
            throw new ValidationMetierException("La quantité constatée ne peut pas être négative.");
        }

        if (string.IsNullOrWhiteSpace(motif))
        {
            throw new ValidationMetierException("Veuillez indiquer le motif de l'ajustement.");
        }

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var inventaire = await _uniteDeTravail.Inventaire
            .ObtenirOuCreerPourMiseAJourAsync(varianteProduitId, jeton)
            .ConfigureAwait(false);

        var ancienneQuantite = inventaire.QuantiteDisponible;
        var ecart = quantiteConstatee - ancienneQuantite;

        if (ecart == 0)
        {
            // Rien à corriger : aucun mouvement n'est créé pour ne pas polluer
            // l'historique avec des lignes sans effet.
            await transaction.ValiderAsync(jeton).ConfigureAwait(false);
            return ancienneQuantite;
        }

        inventaire.QuantiteDisponible = quantiteConstatee;
        inventaire.DateModification = _horodatage.MaintenantUtc;

        await CreerMouvementAsync(
            varianteProduitId,
            ecart,
            ancienneQuantite,
            quantiteConstatee,
            TypeMouvementStock.Ajustement,
            motif,
            null,
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation(
            "Ajustement de stock sur la variante {Variante} : {Ancienne} → {Nouvelle} ({Motif}).",
            varianteProduitId, ancienneQuantite, quantiteConstatee, motif);

        return quantiteConstatee;
    }

    public Task<int> EnregistrerEntreeManuelleAsync(
        int varianteProduitId,
        int quantite,
        TypeMouvementStock typeMouvement,
        string motif,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierStock);
        ExigerMotif(motif);

        return AppliquerVariationAsync(varianteProduitId, quantite, typeMouvement, motif, null, jeton);
    }

    public Task<int> EnregistrerSortieManuelleAsync(
        int varianteProduitId,
        int quantite,
        TypeMouvementStock typeMouvement,
        string motif,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierStock);
        ExigerMotif(motif);

        return AppliquerVariationAsync(varianteProduitId, -quantite, typeMouvement, motif, null, jeton);
    }

    /// <summary>
    /// Cœur de la gestion du stock : verrouille la ligne, contrôle la règle
    /// métier, met à jour la quantité et trace le mouvement. Toutes les
    /// opérations passent par ce chemin, ce qui rend impossible une
    /// modification silencieuse du stock.
    /// </summary>
    private async Task<int> AppliquerVariationAsync(
        int varianteProduitId,
        int variation,
        TypeMouvementStock typeMouvement,
        string? motif,
        string? referenceDocument,
        CancellationToken jeton)
    {
        if (variation == 0)
        {
            throw new ValidationMetierException("La quantité d'un mouvement de stock doit être différente de zéro.");
        }

        // Une transaction est indispensable : le verrou posé sur la ligne
        // d'inventaire n'est tenu que jusqu'à la fin de celle-ci.
        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var inventaire = await _uniteDeTravail.Inventaire
            .ObtenirOuCreerPourMiseAJourAsync(varianteProduitId, jeton)
            .ConfigureAwait(false);

        var ancienneQuantite = inventaire.QuantiteDisponible;
        var nouvelleQuantite = ancienneQuantite + variation;

        if (nouvelleQuantite < 0)
        {
            var designation = await ObtenirDesignationAsync(varianteProduitId, jeton).ConfigureAwait(false);

            throw new StockInsuffisantException(designation, Math.Abs(variation), ancienneQuantite);
        }

        inventaire.QuantiteDisponible = nouvelleQuantite;
        inventaire.DateModification = _horodatage.MaintenantUtc;

        await CreerMouvementAsync(
            varianteProduitId,
            variation,
            ancienneQuantite,
            nouvelleQuantite,
            typeMouvement,
            motif,
            referenceDocument,
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return nouvelleQuantite;
    }

    private async Task CreerMouvementAsync(
        int varianteProduitId,
        int quantite,
        int ancienneQuantite,
        int nouvelleQuantite,
        TypeMouvementStock typeMouvement,
        string? motif,
        string? referenceDocument,
        CancellationToken jeton)
    {
        var mouvement = new MouvementStock
        {
            VarianteProduitId = varianteProduitId,
            TypeMouvement = typeMouvement,
            Quantite = quantite,
            AncienneQuantite = ancienneQuantite,
            NouvelleQuantite = nouvelleQuantite,
            Motif = motif,
            ReferenceDocument = referenceDocument,
            UtilisateurId = _session.UtilisateurId,
            DateCreation = _horodatage.MaintenantUtc
        };

        await _uniteDeTravail.Depot<MouvementStock>().AjouterAsync(mouvement, jeton).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------
    // Consultations
    // ------------------------------------------------------------------

    public async Task<bool> VerifierDisponibiliteAsync(
        int varianteProduitId,
        int quantite,
        CancellationToken jeton = default)
    {
        if (quantite <= 0)
        {
            return true;
        }

        var disponible = await _uniteDeTravail.Inventaire
            .ObtenirQuantiteAsync(varianteProduitId, jeton)
            .ConfigureAwait(false);

        return disponible >= quantite;
    }

    public async Task ExigerDisponibiliteAsync(
        int varianteProduitId,
        int quantite,
        CancellationToken jeton = default)
    {
        var disponible = await _uniteDeTravail.Inventaire
            .ObtenirQuantiteAsync(varianteProduitId, jeton)
            .ConfigureAwait(false);

        if (disponible < quantite)
        {
            var designation = await ObtenirDesignationAsync(varianteProduitId, jeton).ConfigureAwait(false);

            throw new StockInsuffisantException(designation, quantite, disponible);
        }
    }

    public Task<int> ObtenirQuantiteAsync(int varianteProduitId, CancellationToken jeton = default) =>
        _uniteDeTravail.Inventaire.ObtenirQuantiteAsync(varianteProduitId, jeton);

    public async Task<IReadOnlyList<MouvementStockDto>> ObtenirHistoriqueAsync(
        int? varianteProduitId = null,
        DateTime? debut = null,
        DateTime? fin = null,
        TypeMouvementStock? typeMouvement = null,
        int limite = 500,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirStock);

        var requete = _uniteDeTravail.Depot<MouvementStock>().Requete();

        if (varianteProduitId.HasValue)
        {
            requete = requete.Where(m => m.VarianteProduitId == varianteProduitId.Value);
        }

        if (debut.HasValue)
        {
            requete = requete.Where(m => m.DateCreation >= debut.Value);
        }

        if (fin.HasValue)
        {
            requete = requete.Where(m => m.DateCreation <= fin.Value);
        }

        if (typeMouvement.HasValue)
        {
            requete = requete.Where(m => m.TypeMouvement == typeMouvement.Value);
        }

        var mouvements = await requete
            .OrderByDescending(m => m.DateCreation)
            .ThenByDescending(m => m.Id)
            .Take(limite)
            .Select(m => new
            {
                m.Id,
                m.VarianteProduitId,
                ProduitNom = m.VarianteProduit.Produit.Nom,
                Couleur = m.VarianteProduit.Couleur.Nom,
                Taille = m.VarianteProduit.Taille.Nom,
                m.VarianteProduit.SKU,
                m.TypeMouvement,
                m.Quantite,
                m.AncienneQuantite,
                m.NouvelleQuantite,
                m.Motif,
                m.ReferenceDocument,
                NomUtilisateur = m.Utilisateur != null ? m.Utilisateur.Prenom + " " + m.Utilisateur.Nom : null,
                m.DateCreation
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return mouvements.Select(m => new MouvementStockDto
        {
            Id = m.Id,
            VarianteProduitId = m.VarianteProduitId,
            Designation = $"{m.ProduitNom} — {m.Couleur} / {m.Taille}",
            Sku = m.SKU,
            TypeMouvement = m.TypeMouvement,
            LibelleTypeMouvement = m.TypeMouvement.Libelle(),
            Quantite = m.Quantite,
            AncienneQuantite = m.AncienneQuantite,
            NouvelleQuantite = m.NouvelleQuantite,
            Motif = m.Motif,
            ReferenceDocument = m.ReferenceDocument,
            Utilisateur = m.NomUtilisateur,
            DateCreation = m.DateCreation
        }).ToList();
    }

    public async Task<IReadOnlyList<LigneStockDto>> ListerStockAsync(
        string? recherche = null,
        bool uniquementStockFaible = false,
        bool uniquementRuptures = false,
        bool inclureInactifs = false,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirStock);

        var requete = ConstruireRequeteStock(inclureInactifs);

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            // Le code-barres est comparé à l'identique : c'est le chemin rapide
            // du scanner, servi par l'index unique. Les autres champs acceptent
            // une correspondance partielle insensible à la casse.
            var terme = recherche.Trim();
            var termeMinuscule = terme.ToLowerInvariant();

            requete = requete.Where(v =>
                v.CodeBarres == terme
                || v.SKU.ToLower().Contains(termeMinuscule)
                || v.Produit.Nom.ToLower().Contains(termeMinuscule)
                || v.Produit.Reference.ToLower().Contains(termeMinuscule));
        }

        if (uniquementRuptures)
        {
            requete = requete.Where(v => v.Inventaire == null || v.Inventaire.QuantiteDisponible <= 0);
        }
        else if (uniquementStockFaible)
        {
            requete = requete.Where(v =>
                v.Inventaire != null
                && v.Inventaire.QuantiteDisponible > 0
                && v.SeuilMinimum > 0
                && v.Inventaire.QuantiteDisponible <= v.SeuilMinimum);
        }

        return await ProjeterAsync(requete
                .OrderBy(v => v.Produit.Nom)
                .ThenBy(v => v.Couleur.Nom)
                .ThenBy(v => v.Taille.Ordre),
            jeton).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LigneStockDto>> ListerStockFaibleAsync(
        int limite = 100,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirStock);

        var requete = ConstruireRequeteStock(inclureInactifs: false)
            .Where(v => v.Inventaire != null
                        && v.Inventaire.QuantiteDisponible > 0
                        && v.SeuilMinimum > 0
                        && v.Inventaire.QuantiteDisponible <= v.SeuilMinimum)
            .OrderBy(v => v.Inventaire!.QuantiteDisponible)
            .ThenBy(v => v.Produit.Nom)
            .Take(limite);

        return await ProjeterAsync(requete, jeton).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LigneStockDto>> ListerRupturesAsync(
        int limite = 100,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirStock);

        var requete = ConstruireRequeteStock(inclureInactifs: false)
            .Where(v => v.Inventaire == null || v.Inventaire.QuantiteDisponible <= 0)
            .OrderBy(v => v.Produit.Nom)
            .ThenBy(v => v.Couleur.Nom)
            .ThenBy(v => v.Taille.Ordre)
            .Take(limite);

        return await ProjeterAsync(requete, jeton).ConfigureAwait(false);
    }

    public async Task<decimal> CalculerValeurStockAsync(CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirStock);

        // Le prix retenu est celui de la variante lorsqu'il est défini, sinon
        // celui du produit : c'est la même règle que partout ailleurs.
        return await _uniteDeTravail.Variantes.Requete()
            .Where(v => v.Actif && v.Inventaire != null && v.Inventaire.QuantiteDisponible > 0)
            .SumAsync(v => v.Inventaire!.QuantiteDisponible * (v.PrixAchat ?? v.Produit.PrixAchat), jeton)
            .ConfigureAwait(false);
    }

    // ------------------------------------------------------------------
    // Utilitaires internes
    // ------------------------------------------------------------------

    private IQueryable<VarianteProduit> ConstruireRequeteStock(bool inclureInactifs)
    {
        var requete = _uniteDeTravail.Variantes.Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(v => v.Actif && v.Produit.Actif);
        }

        return requete;
    }

    private static async Task<IReadOnlyList<LigneStockDto>> ProjeterAsync(
        IQueryable<VarianteProduit> requete,
        CancellationToken jeton) =>
        await requete
            .Select(v => new LigneStockDto
            {
                VarianteProduitId = v.Id,
                ProduitId = v.ProduitId,
                ProduitNom = v.Produit.Nom,
                Reference = v.Produit.Reference,
                Sku = v.SKU,
                CodeBarres = v.CodeBarres,
                Marque = v.Produit.Marque != null ? v.Produit.Marque.Nom : null,
                Couleur = v.Couleur.Nom,
                CodeCouleur = v.Couleur.CodeCouleur,
                Taille = v.Taille.Nom,
                OrdreTaille = v.Taille.Ordre,
                QuantiteDisponible = v.Inventaire != null ? v.Inventaire.QuantiteDisponible : 0,
                SeuilMinimum = v.SeuilMinimum,
                PrixAchat = v.PrixAchat ?? v.Produit.PrixAchat,
                PrixVente = v.PrixVente ?? v.Produit.PrixVente,
                Actif = v.Actif
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

    private async Task<string> ObtenirDesignationAsync(int varianteProduitId, CancellationToken jeton)
    {
        var designation = await _uniteDeTravail.Variantes.Requete()
            .Where(v => v.Id == varianteProduitId)
            .Select(v => v.Produit.Nom + " — " + v.Couleur.Nom + " / " + v.Taille.Nom)
            .FirstOrDefaultAsync(jeton)
            .ConfigureAwait(false);

        return designation ?? $"Article n° {varianteProduitId}";
    }

    private static void ExigerMotif(string motif)
    {
        if (string.IsNullOrWhiteSpace(motif))
        {
            throw new ValidationMetierException("Veuillez indiquer le motif du mouvement de stock.");
        }
    }
}
