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

/// <inheritdoc cref="IServiceProduits"/>
public class ServiceProduits : IServiceProduits
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IServiceStock _stock;
    private readonly IServiceAudit _audit;
    private readonly ISessionUtilisateur _session;
    private readonly IFournisseurHorodatage _horodatage;
    private readonly ILogger<ServiceProduits> _journal;

    public ServiceProduits(
        IUniteDeTravail uniteDeTravail,
        IServiceStock stock,
        IServiceAudit audit,
        ISessionUtilisateur session,
        IFournisseurHorodatage horodatage,
        ILogger<ServiceProduits> journal)
    {
        _uniteDeTravail = uniteDeTravail;
        _stock = stock;
        _audit = audit;
        _session = session;
        _horodatage = horodatage;
        _journal = journal;
    }

    // ==================================================================
    // Produits
    // ==================================================================

    public async Task<ProduitDto> CreerProduitAsync(DemandeProduit demande, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        await ValiderProduitAsync(demande, null, jeton).ConfigureAwait(false);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var produit = new Produit
        {
            Reference = demande.Reference.Trim(),
            SKU = demande.Sku.Trim(),
            Nom = demande.Nom.Trim(),
            Description = demande.Description?.Trim(),
            MarqueId = demande.MarqueId,
            CategorieId = demande.CategorieId,
            Collection = demande.Collection?.Trim(),
            Saison = demande.Saison?.Trim(),
            PrixAchat = demande.PrixAchat,
            PrixVente = demande.PrixVente,
            Photo = demande.Photo,
            Actif = true,
            DateCreation = _horodatage.MaintenantUtc
        };

        await _uniteDeTravail.Depot<Produit>().AjouterAsync(produit, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        await _audit.TracerAsync(
            ActionsAudit.Creation,
            nameof(Produit),
            produit.Id,
            $"Produit « {produit.Nom} » créé (référence {produit.Reference}).",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation("Produit {Reference} créé.", produit.Reference);

        return await ObtenirProduitObligatoireAsync(produit.Id, jeton).ConfigureAwait(false);
    }

    public async Task<ProduitDto> ModifierProduitAsync(
        int produitId,
        DemandeProduit demande,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        await ValiderProduitAsync(demande, produitId, jeton).ConfigureAwait(false);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var produit = await _uniteDeTravail.Depot<Produit>().RequeteSuivie()
            .FirstOrDefaultAsync(p => p.Id == produitId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Produit", produitId);

        var ancienPrixVente = produit.PrixVente;
        var ancienPrixAchat = produit.PrixAchat;
        var prixModifie = ancienPrixVente != demande.PrixVente || ancienPrixAchat != demande.PrixAchat;

        // Changer un tarif est une action sensible : elle exige sa propre
        // autorisation, distincte de la simple modification de fiche.
        if (prixModifie)
        {
            _session.ExigerPermission(CodesPermissions.ModifierPrix);
        }

        produit.Reference = demande.Reference.Trim();
        produit.SKU = demande.Sku.Trim();
        produit.Nom = demande.Nom.Trim();
        produit.Description = demande.Description?.Trim();
        produit.MarqueId = demande.MarqueId;
        produit.CategorieId = demande.CategorieId;
        produit.Collection = demande.Collection?.Trim();
        produit.Saison = demande.Saison?.Trim();
        produit.PrixAchat = demande.PrixAchat;
        produit.PrixVente = demande.PrixVente;
        produit.Photo = demande.Photo;
        produit.DateModification = _horodatage.MaintenantUtc;

        await _audit.TracerAsync(
            ActionsAudit.Modification,
            nameof(Produit),
            produit.Id,
            $"Produit « {produit.Nom} » modifié.",
            jeton).ConfigureAwait(false);

        if (prixModifie)
        {
            await _audit.TracerAsync(
                ActionsAudit.ChangementPrix,
                nameof(Produit),
                produit.Id,
                $"Prix de « {produit.Nom} » modifiés : achat {ancienPrixAchat:N2} → {demande.PrixAchat:N2} DA, " +
                $"vente {ancienPrixVente:N2} → {demande.PrixVente:N2} DA.",
                jeton).ConfigureAwait(false);
        }

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirProduitObligatoireAsync(produitId, jeton).ConfigureAwait(false);
    }

    public async Task<ProduitDto> DefinirEtatProduitAsync(
        int produitId,
        bool actif,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.SupprimerProduits);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var produit = await _uniteDeTravail.Depot<Produit>().RequeteSuivie()
            .FirstOrDefaultAsync(p => p.Id == produitId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Produit", produitId);

        produit.Actif = actif;
        produit.DateModification = _horodatage.MaintenantUtc;

        await _audit.TracerAsync(
            actif ? ActionsAudit.Activation : ActionsAudit.Desactivation,
            nameof(Produit),
            produit.Id,
            actif
                ? $"Produit « {produit.Nom} » réactivé."
                : $"Produit « {produit.Nom} » désactivé : il n'est plus proposé à la vente.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirProduitObligatoireAsync(produitId, jeton).ConfigureAwait(false);
    }

    public async Task<ProduitDto?> ObtenirProduitAsync(int produitId, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirProduits);

        var produit = await _uniteDeTravail.Depot<Produit>().Requete()
            .Include(p => p.Marque)
            .Include(p => p.Categorie).ThenInclude(c => c.SystemeTaille)
            .Include(p => p.Variantes).ThenInclude(v => v.Taille)
            .Include(p => p.Variantes).ThenInclude(v => v.Couleur)
            .Include(p => p.Variantes).ThenInclude(v => v.Inventaire)
            .FirstOrDefaultAsync(p => p.Id == produitId, jeton)
            .ConfigureAwait(false);

        return produit is null ? null : Projeter(produit);
    }

    public async Task<IReadOnlyList<ResumeProduitDto>> RechercherProduitsAsync(
        string? recherche = null,
        int? marqueId = null,
        string? collection = null,
        string? saison = null,
        bool inclureInactifs = false,
        int limite = 500,
        int? categorieId = null,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirProduits);

        var requete = _uniteDeTravail.Depot<Produit>().Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(p => p.Actif);
        }

        if (marqueId.HasValue)
        {
            requete = requete.Where(p => p.MarqueId == marqueId.Value);
        }

        if (categorieId.HasValue)
        {
            requete = requete.Where(p => p.CategorieId == categorieId.Value);
        }

        if (!string.IsNullOrWhiteSpace(collection))
        {
            requete = requete.Where(p => p.Collection == collection);
        }

        if (!string.IsNullOrWhiteSpace(saison))
        {
            requete = requete.Where(p => p.Saison == saison);
        }

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var terme = recherche.Trim();
            var termeMinuscule = terme.ToLowerInvariant();

            requete = requete.Where(p =>
                p.Nom.ToLower().Contains(termeMinuscule)
                || p.Reference.ToLower().Contains(termeMinuscule)
                || p.SKU.ToLower().Contains(termeMinuscule)
                || p.Variantes.Any(v => v.CodeBarres == terme || v.SKU.ToLower().Contains(termeMinuscule)));
        }

        return await requete
            .OrderBy(p => p.Nom)
            .Take(limite)
            .Select(p => new ResumeProduitDto
            {
                Id = p.Id,
                Reference = p.Reference,
                Sku = p.SKU,
                Nom = p.Nom,
                Marque = p.Marque != null ? p.Marque.Nom : null,
                Categorie = p.Categorie.Nom,
                Collection = p.Collection,
                Saison = p.Saison,
                PrixAchat = p.PrixAchat,
                PrixVente = p.PrixVente,
                NombreVariantes = p.Variantes.Count,
                StockTotal = p.Variantes
                    .Where(v => v.Inventaire != null)
                    .Sum(v => (int?)v.Inventaire!.QuantiteDisponible) ?? 0,
                Actif = p.Actif
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    // ==================================================================
    // Déclinaisons
    // ==================================================================

    public async Task<VarianteDto> AjouterVarianteAsync(
        int produitId,
        DemandeVariante demande,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var produit = await _uniteDeTravail.Depot<Produit>().Requete()
            .FirstOrDefaultAsync(p => p.Id == produitId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Produit", produitId);

        await ValiderVarianteAsync(produitId, demande, null, jeton).ConfigureAwait(false);

        var sku = string.IsNullOrWhiteSpace(demande.Sku)
            ? await ComposerSkuAsync(produit, demande.TailleId, demande.CouleurId, jeton).ConfigureAwait(false)
            : demande.Sku.Trim();

        var variante = new VarianteProduit
        {
            ProduitId = produitId,
            TailleId = demande.TailleId,
            CouleurId = demande.CouleurId,
            SKU = sku,
            CodeBarres = string.IsNullOrWhiteSpace(demande.CodeBarres) ? null : demande.CodeBarres.Trim(),
            PrixAchat = demande.PrixAchat,
            PrixVente = demande.PrixVente,
            SeuilMinimum = demande.SeuilMinimum,
            Actif = true
        };

        await _uniteDeTravail.Depot<VarianteProduit>().AjouterAsync(variante, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        // Chaque déclinaison reçoit immédiatement sa ligne de stock à zéro :
        // l'inventaire est ainsi toujours complet et interrogeable.
        await _uniteDeTravail.Depot<Inventaire>().AjouterAsync(new Inventaire
        {
            VarianteProduitId = variante.Id,
            QuantiteDisponible = 0,
            DateModification = _horodatage.MaintenantUtc
        }, jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        await _audit.TracerAsync(
            ActionsAudit.Creation,
            nameof(VarianteProduit),
            variante.Id,
            $"Déclinaison {sku} ajoutée au produit « {produit.Nom} ».",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        // La quantité initiale passe par le service de stock : elle laisse donc
        // une trace dans l'historique, comme n'importe quelle autre entrée.
        if (demande.QuantiteInitiale > 0)
        {
            await _stock.AjouterStockAsync(
                variante.Id,
                demande.QuantiteInitiale,
                TypeMouvementStock.Ajustement,
                "Stock initial à la création de la déclinaison",
                null,
                jeton).ConfigureAwait(false);
        }

        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirVarianteObligatoireAsync(variante.Id, jeton).ConfigureAwait(false);
    }

    public async Task<VarianteDto> ModifierVarianteAsync(
        int varianteId,
        DemandeVariante demande,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var variante = await _uniteDeTravail.Depot<VarianteProduit>().RequeteSuivie()
            .Include(v => v.Produit)
            .FirstOrDefaultAsync(v => v.Id == varianteId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Déclinaison", varianteId);

        await ValiderVarianteAsync(variante.ProduitId, demande, varianteId, jeton).ConfigureAwait(false);

        if (variante.PrixAchat != demande.PrixAchat || variante.PrixVente != demande.PrixVente)
        {
            _session.ExigerPermission(CodesPermissions.ModifierPrix);
        }

        variante.TailleId = demande.TailleId;
        variante.CouleurId = demande.CouleurId;
        variante.CodeBarres = string.IsNullOrWhiteSpace(demande.CodeBarres) ? null : demande.CodeBarres.Trim();
        variante.PrixAchat = demande.PrixAchat;
        variante.PrixVente = demande.PrixVente;
        variante.SeuilMinimum = demande.SeuilMinimum;

        if (!string.IsNullOrWhiteSpace(demande.Sku))
        {
            variante.SKU = demande.Sku.Trim();
        }

        await _audit.TracerAsync(
            ActionsAudit.Modification,
            nameof(VarianteProduit),
            variante.Id,
            $"Déclinaison {variante.SKU} modifiée.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirVarianteObligatoireAsync(varianteId, jeton).ConfigureAwait(false);
    }

    public async Task<VarianteDto> DefinirEtatVarianteAsync(
        int varianteId,
        bool actif,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var variante = await _uniteDeTravail.Depot<VarianteProduit>().RequeteSuivie()
            .FirstOrDefaultAsync(v => v.Id == varianteId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Déclinaison", varianteId);

        variante.Actif = actif;

        await _audit.TracerAsync(
            actif ? ActionsAudit.Activation : ActionsAudit.Desactivation,
            nameof(VarianteProduit),
            variante.Id,
            actif
                ? $"Déclinaison {variante.SKU} réactivée."
                : $"Déclinaison {variante.SKU} désactivée.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirVarianteObligatoireAsync(varianteId, jeton).ConfigureAwait(false);
    }

    public async Task<int> GenererVariantesAsync(
        int produitId,
        IReadOnlyList<int> tailleIds,
        IReadOnlyList<int> couleurIds,
        int seuilMinimum = 0,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        if (tailleIds.Count == 0 || couleurIds.Count == 0)
        {
            throw new ValidationMetierException(
                "Sélectionnez au moins une taille et une couleur pour générer les déclinaisons.");
        }

        var produit = await _uniteDeTravail.Depot<Produit>().Requete()
            .FirstOrDefaultAsync(p => p.Id == produitId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Produit", produitId);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        // Les combinaisons déjà présentes sont écartées : la génération peut
        // être relancée après l'ajout d'une couleur sans créer de doublon.
        var existantes = await _uniteDeTravail.Variantes.Requete()
            .Where(v => v.ProduitId == produitId)
            .Select(v => new { v.TailleId, v.CouleurId })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var dejaPresentes = existantes
            .Select(v => (v.TailleId, v.CouleurId))
            .ToHashSet();

        var nouvelles = new List<VarianteProduit>();

        foreach (var couleurId in couleurIds)
        {
            foreach (var tailleId in tailleIds)
            {
                if (dejaPresentes.Contains((tailleId, couleurId)))
                {
                    continue;
                }

                nouvelles.Add(new VarianteProduit
                {
                    ProduitId = produitId,
                    TailleId = tailleId,
                    CouleurId = couleurId,
                    SKU = await ComposerSkuAsync(produit, tailleId, couleurId, jeton).ConfigureAwait(false),
                    SeuilMinimum = seuilMinimum,
                    Actif = true
                });
            }
        }

        if (nouvelles.Count == 0)
        {
            await transaction.ValiderAsync(jeton).ConfigureAwait(false);
            return 0;
        }

        await _uniteDeTravail.Depot<VarianteProduit>().AjouterPlusieursAsync(nouvelles, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        await _uniteDeTravail.Depot<Inventaire>().AjouterPlusieursAsync(
            nouvelles.Select(v => new Inventaire
            {
                VarianteProduitId = v.Id,
                QuantiteDisponible = 0,
                DateModification = _horodatage.MaintenantUtc
            }),
            jeton).ConfigureAwait(false);

        await _audit.TracerAsync(
            ActionsAudit.Creation,
            nameof(VarianteProduit),
            produitId,
            $"{nouvelles.Count} déclinaison(s) générée(s) pour le produit « {produit.Nom} ».",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation(
            "{Nombre} déclinaisons générées pour le produit {Produit}.", nouvelles.Count, produit.Reference);

        return nouvelles.Count;
    }

    public async Task<VarianteDto?> ObtenirVarianteParCodeBarresAsync(
        string codeBarres,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirProduits);

        if (string.IsNullOrWhiteSpace(codeBarres))
        {
            return null;
        }

        var variante = await _uniteDeTravail.Variantes
            .ObtenirParCodeBarresAsync(codeBarres, jeton)
            .ConfigureAwait(false);

        return variante is null ? null : ProjeterVariante(variante);
    }

    public async Task<IReadOnlyList<VarianteDto>> RechercherVariantesAsync(
        string terme,
        int limite = 50,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirProduits);

        var variantes = await _uniteDeTravail.Variantes
            .RechercherAsync(terme, limite, jeton)
            .ConfigureAwait(false);

        return variantes.Select(v => ProjeterVariante(v)).ToList();
    }

    public async Task<IReadOnlyList<VarianteDto>> ListerVendablesAsync(
        int limite = 400,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirProduits);

        var variantes = await _uniteDeTravail.Variantes
            .ListerVendablesAsync(limite, jeton)
            .ConfigureAwait(false);

        return variantes.Select(v => ProjeterVariante(v)).ToList();
    }

    // ==================================================================
    // Données de référence
    // ==================================================================

    public async Task<IReadOnlyList<ReferenceDto>> ListerMarquesAsync(
        bool inclureInactifs = false,
        CancellationToken jeton = default)
    {
        var requete = _uniteDeTravail.Depot<Marque>().Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(m => m.Actif);
        }

        return await requete
            .OrderBy(m => m.Nom)
            .Select(m => new ReferenceDto
            {
                Id = m.Id,
                Nom = m.Nom,
                Complement = m.Description,
                Actif = m.Actif,
                NombreUtilisations = m.Produits.Count
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Familles d'articles, avec le système de tailles de chacune.
    /// </summary>
    public async Task<IReadOnlyList<CategorieDto>> ListerCategoriesAsync(
        bool inclureInactifs = false,
        CancellationToken jeton = default)
    {
        var requete = _uniteDeTravail.Depot<Categorie>().Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(c => c.Actif);
        }

        return await requete
            .OrderBy(c => c.Ordre)
            .ThenBy(c => c.Nom)
            .Select(c => new CategorieDto
            {
                Id = c.Id,
                Nom = c.Nom,
                SystemeTailleId = c.SystemeTailleId,
                SystemeTaille = c.SystemeTaille.Nom,
                Ordre = c.Ordre,
                Actif = c.Actif,
                NombreProduits = c.Produits.Count
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    /// <summary>Systèmes de tailles disponibles.</summary>
    public async Task<IReadOnlyList<ReferenceDto>> ListerSystemesTaillesAsync(
        bool inclureInactifs = false,
        CancellationToken jeton = default)
    {
        var requete = _uniteDeTravail.Depot<SystemeTaille>().Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(s => s.Actif);
        }

        return await requete
            .OrderBy(s => s.Ordre)
            .ThenBy(s => s.Nom)
            .Select(s => new ReferenceDto
            {
                Id = s.Id,
                Nom = s.Nom,
                Ordre = s.Ordre,
                Actif = s.Actif,
                NombreUtilisations = s.Tailles.Count
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Tailles disponibles. Le système restreint la liste à celles qui ont un
    /// sens pour la famille de l'article : proposer « XXL » sur une chaussure
    /// laisserait créer une déclinaison invendable, et le stock la garderait.
    /// </summary>
    public async Task<IReadOnlyList<ReferenceDto>> ListerTaillesAsync(
        bool inclureInactifs = false,
        CancellationToken jeton = default,
        int? systemeTailleId = null)
    {
        var requete = _uniteDeTravail.Depot<Taille>().Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(t => t.Actif);
        }

        if (systemeTailleId.HasValue)
        {
            requete = requete.Where(t => t.SystemeTailleId == systemeTailleId.Value);
        }

        return await requete
            .OrderBy(t => t.Ordre)
            .ThenBy(t => t.Nom)
            .Select(t => new ReferenceDto
            {
                Id = t.Id,
                Nom = t.Nom,
                Complement = t.SystemeTaille.Nom,
                Ordre = t.Ordre,
                Actif = t.Actif,
                NombreUtilisations = t.Variantes.Count
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReferenceDto>> ListerCouleursAsync(
        bool inclureInactifs = false,
        CancellationToken jeton = default)
    {
        var requete = _uniteDeTravail.Depot<Couleur>().Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(c => c.Actif);
        }

        return await requete
            .OrderBy(c => c.Nom)
            .Select(c => new ReferenceDto
            {
                Id = c.Id,
                Nom = c.Nom,
                Complement = c.CodeCouleur,
                Actif = c.Actif,
                NombreUtilisations = c.Variantes.Count
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    public async Task<ReferenceDto> EnregistrerMarqueAsync(
        int? id,
        string nom,
        string? description,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new ValidationMetierException("Le nom de la marque est obligatoire.");
        }

        var nomNettoye = nom.Trim();

        var doublon = await _uniteDeTravail.Depot<Marque>().Requete()
            .AnyAsync(m => m.Nom.ToLower() == nomNettoye.ToLower() && (id == null || m.Id != id), jeton)
            .ConfigureAwait(false);

        if (doublon)
        {
            throw new ValidationMetierException($"La marque « {nomNettoye} » existe déjà.");
        }

        Marque marque;

        if (id.HasValue)
        {
            marque = await _uniteDeTravail.Depot<Marque>().RequeteSuivie()
                .FirstOrDefaultAsync(m => m.Id == id.Value, jeton)
                .ConfigureAwait(false)
                ?? throw new EntiteIntrouvableException("Marque", id.Value);

            marque.Nom = nomNettoye;
            marque.Description = description?.Trim();
        }
        else
        {
            marque = new Marque { Nom = nomNettoye, Description = description?.Trim(), Actif = true };
            await _uniteDeTravail.Depot<Marque>().AjouterAsync(marque, jeton).ConfigureAwait(false);
        }

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return new ReferenceDto
        {
            Id = marque.Id,
            Nom = marque.Nom,
            Complement = marque.Description,
            Actif = marque.Actif
        };
    }

    /// <summary>
    /// Crée ou renomme une taille.
    ///
    /// Une taille appartient toujours à un système : « 42 » ne veut pas dire
    /// la même chose sur une chaussure et sur un pantalon. Le nom n'est donc
    /// unique qu'à l'intérieur de son système.
    /// </summary>
    /// <summary>
    /// Crée ou renomme une famille d'articles.
    ///
    /// Changer le système de tailles d'une famille qui porte déjà des
    /// articles est refusé : les déclinaisons existantes deviendraient
    /// étrangères à leur propre famille, et le stock garderait des tailles
    /// que plus rien ne proposerait.
    /// </summary>
    public async Task<CategorieDto> EnregistrerCategorieAsync(
        int? id,
        string nom,
        int systemeTailleId,
        int ordre,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new ValidationMetierException("Le nom de la famille est obligatoire.");
        }

        var nomNettoye = nom.Trim();

        if (!await _uniteDeTravail.Depot<SystemeTaille>().Requete()
                .AnyAsync(t => t.Id == systemeTailleId, jeton).ConfigureAwait(false))
        {
            throw new ValidationMetierException("Le système de tailles sélectionné n'existe pas.");
        }

        var doublon = await _uniteDeTravail.Depot<Categorie>().Requete()
            .AnyAsync(c => c.Nom.ToLower() == nomNettoye.ToLower() && (id == null || c.Id != id), jeton)
            .ConfigureAwait(false);

        if (doublon)
        {
            throw new ValidationMetierException($"La famille « {nomNettoye} » existe déjà.");
        }

        Categorie categorie;

        if (id.HasValue)
        {
            categorie = await _uniteDeTravail.Depot<Categorie>().RequeteSuivie()
                .FirstOrDefaultAsync(c => c.Id == id.Value, jeton)
                .ConfigureAwait(false)
                ?? throw new EntiteIntrouvableException("Famille", id.Value);

            if (categorie.SystemeTailleId != systemeTailleId
                && await _uniteDeTravail.Depot<Produit>().Requete()
                    .AnyAsync(p => p.CategorieId == id.Value, jeton).ConfigureAwait(false))
            {
                throw new ValidationMetierException(
                    $"La famille « {categorie.Nom} » porte déjà des articles : son système de " +
                    "tailles ne peut plus être changé. Créez une nouvelle famille.");
            }

            categorie.Nom = nomNettoye;
            categorie.SystemeTailleId = systemeTailleId;
            categorie.Ordre = ordre;
        }
        else
        {
            categorie = new Categorie
            {
                Nom = nomNettoye,
                SystemeTailleId = systemeTailleId,
                Ordre = ordre,
                Actif = true
            };

            await _uniteDeTravail.Depot<Categorie>().AjouterAsync(categorie, jeton).ConfigureAwait(false);
        }

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return new CategorieDto
        {
            Id = categorie.Id,
            Nom = categorie.Nom,
            SystemeTailleId = categorie.SystemeTailleId,
            Ordre = categorie.Ordre,
            Actif = categorie.Actif
        };
    }

    /// <summary>
    /// Active ou désactive une famille. Une famille inactive n'est plus
    /// proposée à la création d'un article, mais ses articles restent en
    /// place : rien de l'historique ne disparaît.
    /// </summary>
    public async Task<CategorieDto> DefinirEtatCategorieAsync(
        int categorieId,
        bool actif,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        var categorie = await _uniteDeTravail.Depot<Categorie>().RequeteSuivie()
            .FirstOrDefaultAsync(c => c.Id == categorieId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Famille", categorieId);

        categorie.Actif = actif;

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return new CategorieDto
        {
            Id = categorie.Id,
            Nom = categorie.Nom,
            SystemeTailleId = categorie.SystemeTailleId,
            Ordre = categorie.Ordre,
            Actif = categorie.Actif
        };
    }

    public async Task<ReferenceDto> EnregistrerTailleAsync(
        int? id,
        string nom,
        int ordre,
        CancellationToken jeton = default,
        int? systemeTailleId = null)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new ValidationMetierException("Le nom de la taille est obligatoire.");
        }

        var nomNettoye = nom.Trim();

        Taille taille;

        if (id.HasValue)
        {
            taille = await _uniteDeTravail.Depot<Taille>().RequeteSuivie()
                .FirstOrDefaultAsync(t => t.Id == id.Value, jeton)
                .ConfigureAwait(false)
                ?? throw new EntiteIntrouvableException("Taille", id.Value);

            // Déplacer une taille d'un système à l'autre changerait le sens
            // de toutes les déclinaisons qui l'emploient : le système n'est
            // modifiable que s'il est explicitement fourni.
            var systemeCible = systemeTailleId ?? taille.SystemeTailleId;

            await ExigerTailleUniqueAsync(systemeCible, nomNettoye, id, jeton).ConfigureAwait(false);

            taille.Nom = nomNettoye;
            taille.Ordre = ordre;
            taille.SystemeTailleId = systemeCible;
        }
        else
        {
            if (systemeTailleId is not > 0)
            {
                throw new ValidationMetierException(
                    "Le système de tailles est obligatoire : une taille ne veut rien dire seule.");
            }

            await ExigerTailleUniqueAsync(systemeTailleId.Value, nomNettoye, null, jeton).ConfigureAwait(false);

            taille = new Taille
            {
                Nom = nomNettoye,
                Ordre = ordre,
                SystemeTailleId = systemeTailleId.Value,
                Actif = true
            };

            await _uniteDeTravail.Depot<Taille>().AjouterAsync(taille, jeton).ConfigureAwait(false);
        }

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return new ReferenceDto { Id = taille.Id, Nom = taille.Nom, Ordre = taille.Ordre, Actif = taille.Actif };
    }

    private async Task ExigerTailleUniqueAsync(
        int systemeTailleId,
        string nom,
        int? idExistant,
        CancellationToken jeton)
    {
        var doublon = await _uniteDeTravail.Depot<Taille>().Requete()
            .AnyAsync(
                t => t.SystemeTailleId == systemeTailleId
                     && t.Nom.ToLower() == nom.ToLower()
                     && (idExistant == null || t.Id != idExistant),
                jeton)
            .ConfigureAwait(false);

        if (doublon)
        {
            throw new ValidationMetierException(
                $"La taille « {nom} » existe déjà dans ce système de tailles.");
        }
    }

    public async Task<ReferenceDto> EnregistrerCouleurAsync(
        int? id,
        string nom,
        string? codeCouleur,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new ValidationMetierException("Le nom de la couleur est obligatoire.");
        }

        var nomNettoye = nom.Trim();

        var doublon = await _uniteDeTravail.Depot<Couleur>().Requete()
            .AnyAsync(c => c.Nom.ToLower() == nomNettoye.ToLower() && (id == null || c.Id != id), jeton)
            .ConfigureAwait(false);

        if (doublon)
        {
            throw new ValidationMetierException($"La couleur « {nomNettoye} » existe déjà.");
        }

        Couleur couleur;

        if (id.HasValue)
        {
            couleur = await _uniteDeTravail.Depot<Couleur>().RequeteSuivie()
                .FirstOrDefaultAsync(c => c.Id == id.Value, jeton)
                .ConfigureAwait(false)
                ?? throw new EntiteIntrouvableException("Couleur", id.Value);

            couleur.Nom = nomNettoye;
            couleur.CodeCouleur = codeCouleur?.Trim();
        }
        else
        {
            couleur = new Couleur { Nom = nomNettoye, CodeCouleur = codeCouleur?.Trim(), Actif = true };
            await _uniteDeTravail.Depot<Couleur>().AjouterAsync(couleur, jeton).ConfigureAwait(false);
        }

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return new ReferenceDto
        {
            Id = couleur.Id,
            Nom = couleur.Nom,
            Complement = couleur.CodeCouleur,
            Actif = couleur.Actif
        };
    }

    public Task DefinirEtatMarqueAsync(int id, bool actif, CancellationToken jeton = default) =>
        DefinirEtatReferenceAsync<Marque>(id, actif, m => m.Actif = actif, "Marque", jeton);

    public Task DefinirEtatTailleAsync(int id, bool actif, CancellationToken jeton = default) =>
        DefinirEtatReferenceAsync<Taille>(id, actif, t => t.Actif = actif, "Taille", jeton);

    public Task DefinirEtatCouleurAsync(int id, bool actif, CancellationToken jeton = default) =>
        DefinirEtatReferenceAsync<Couleur>(id, actif, c => c.Actif = actif, "Couleur", jeton);

    public async Task<IReadOnlyList<string>> ListerCollectionsAsync(CancellationToken jeton = default) =>
        await _uniteDeTravail.Depot<Produit>().Requete()
            .Where(p => p.Collection != null && p.Collection != "")
            .Select(p => p.Collection!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<string>> ListerSaisonsAsync(CancellationToken jeton = default) =>
        await _uniteDeTravail.Depot<Produit>().Requete()
            .Where(p => p.Saison != null && p.Saison != "")
            .Select(p => p.Saison!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

    // ==================================================================
    // Utilitaires internes
    // ==================================================================

    private async Task DefinirEtatReferenceAsync<T>(
        int id,
        bool actif,
        Action<T> appliquer,
        string libelleType,
        CancellationToken jeton) where T : Domain.Common.EntiteBase
    {
        _session.ExigerPermission(CodesPermissions.ModifierProduits);

        var entite = await _uniteDeTravail.Depot<T>().RequeteSuivie()
            .FirstOrDefaultAsync(e => e.Id == id, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException(libelleType, id);

        appliquer(entite);

        await _audit.TracerAsync(
            actif ? ActionsAudit.Activation : ActionsAudit.Desactivation,
            libelleType,
            id,
            actif ? $"{libelleType} réactivée." : $"{libelleType} désactivée.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
    }

    private async Task ValiderProduitAsync(DemandeProduit demande, int? idExistant, CancellationToken jeton)
    {
        var erreurs = new List<string>();

        if (string.IsNullOrWhiteSpace(demande.Nom))
        {
            erreurs.Add("Le nom du produit est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(demande.Reference))
        {
            erreurs.Add("La référence du produit est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(demande.Sku))
        {
            erreurs.Add("Le code article (SKU) est obligatoire.");
        }

        if (demande.PrixAchat < 0)
        {
            erreurs.Add("Le prix d'achat ne peut pas être négatif.");
        }

        if (demande.PrixVente < 0)
        {
            erreurs.Add("Le prix de vente ne peut pas être négatif.");
        }

        // Sans famille, la génération des déclinaisons ne saurait pas quelles
        // tailles proposer, et le rayon n'aurait plus de sens.
        if (demande.CategorieId <= 0)
        {
            erreurs.Add("La famille de l'article est obligatoire.");
        }
        else if (!await _uniteDeTravail.Depot<Categorie>().Requete()
                     .AnyAsync(c => c.Id == demande.CategorieId, jeton).ConfigureAwait(false))
        {
            erreurs.Add("La famille sélectionnée n'existe pas.");
        }

        if (erreurs.Count > 0)
        {
            throw new ValidationMetierException(erreurs);
        }

        var reference = demande.Reference.Trim().ToLowerInvariant();
        var sku = demande.Sku.Trim().ToLowerInvariant();

        var depot = _uniteDeTravail.Depot<Produit>();

        if (await depot.Requete().AnyAsync(
                p => p.Reference.ToLower() == reference && (idExistant == null || p.Id != idExistant),
                jeton).ConfigureAwait(false))
        {
            erreurs.Add($"La référence « {demande.Reference.Trim()} » est déjà utilisée par un autre produit.");
        }

        if (await depot.Requete().AnyAsync(
                p => p.SKU.ToLower() == sku && (idExistant == null || p.Id != idExistant),
                jeton).ConfigureAwait(false))
        {
            erreurs.Add($"Le code article « {demande.Sku.Trim()} » est déjà utilisé par un autre produit.");
        }

        if (demande.MarqueId.HasValue
            && !await _uniteDeTravail.Depot<Marque>().Requete()
                .AnyAsync(m => m.Id == demande.MarqueId.Value, jeton).ConfigureAwait(false))
        {
            erreurs.Add("La marque sélectionnée n'existe pas.");
        }

        if (erreurs.Count > 0)
        {
            throw new ValidationMetierException(erreurs);
        }
    }

    private async Task ValiderVarianteAsync(
        int produitId,
        DemandeVariante demande,
        int? idExistant,
        CancellationToken jeton)
    {
        var erreurs = new List<string>();

        if (demande.SeuilMinimum < 0)
        {
            erreurs.Add("Le seuil d'alerte ne peut pas être négatif.");
        }

        if (demande.PrixAchat is < 0)
        {
            erreurs.Add("Le prix d'achat ne peut pas être négatif.");
        }

        if (demande.PrixVente is < 0)
        {
            erreurs.Add("Le prix de vente ne peut pas être négatif.");
        }

        if (demande.QuantiteInitiale < 0)
        {
            erreurs.Add("La quantité initiale ne peut pas être négative.");
        }

        // La taille doit appartenir au système de la famille de l'article.
        // Sans ce contrôle, une chaussure pourrait être créée en XXL par
        // l'import d'un fichier ou une combinaison d'écrans : la déclinaison
        // serait invendable, et le stock la garderait indéfiniment.
        var systemeAttendu = await _uniteDeTravail.Depot<Produit>().Requete()
            .Where(p => p.Id == produitId)
            .Select(p => (int?)p.Categorie.SystemeTailleId)
            .FirstOrDefaultAsync(jeton)
            .ConfigureAwait(false);

        var taille = await _uniteDeTravail.Depot<Taille>().Requete()
            .Where(t => t.Id == demande.TailleId)
            .Select(t => new { t.Nom, t.SystemeTailleId, Systeme = t.SystemeTaille.Nom })
            .FirstOrDefaultAsync(jeton)
            .ConfigureAwait(false);

        if (taille is null)
        {
            erreurs.Add("La taille sélectionnée n'existe pas.");
        }
        else if (systemeAttendu is not null && taille.SystemeTailleId != systemeAttendu)
        {
            erreurs.Add(
                $"La taille « {taille.Nom} » appartient au système « {taille.Systeme} », " +
                "qui n'est pas celui de la famille de cet article.");
        }

        if (!await _uniteDeTravail.Depot<Couleur>().Requete()
                .AnyAsync(c => c.Id == demande.CouleurId, jeton).ConfigureAwait(false))
        {
            erreurs.Add("La couleur sélectionnée n'existe pas.");
        }

        // Contrôle du doublon avant d'atteindre la base : le message affiché
        // est ainsi compréhensible plutôt qu'une erreur de contrainte SQL.
        if (await _uniteDeTravail.Variantes.CombinaisonExisteAsync(
                produitId, demande.TailleId, demande.CouleurId, idExistant, jeton).ConfigureAwait(false))
        {
            erreurs.Add("Cette combinaison de taille et de couleur existe déjà pour ce produit.");
        }

        if (!string.IsNullOrWhiteSpace(demande.CodeBarres)
            && await _uniteDeTravail.Variantes.CodeBarresExisteAsync(
                demande.CodeBarres, idExistant, jeton).ConfigureAwait(false))
        {
            erreurs.Add($"Le code-barres « {demande.CodeBarres.Trim()} » est déjà attribué à un autre article.");
        }

        if (!string.IsNullOrWhiteSpace(demande.Sku)
            && await _uniteDeTravail.Variantes.SkuExisteAsync(
                demande.Sku, idExistant, jeton).ConfigureAwait(false))
        {
            erreurs.Add($"Le code article « {demande.Sku.Trim()} » est déjà utilisé.");
        }

        if (erreurs.Count > 0)
        {
            throw new ValidationMetierException(erreurs);
        }
    }

    /// <summary>
    /// Compose un code article lisible du type « REF-NOI-M ». Un suffixe
    /// numérique est ajouté en cas de collision improbable.
    /// </summary>
    private async Task<string> ComposerSkuAsync(
        Produit produit,
        int tailleId,
        int couleurId,
        CancellationToken jeton)
    {
        var taille = await _uniteDeTravail.Depot<Taille>().Requete()
            .Where(t => t.Id == tailleId).Select(t => t.Nom).FirstAsync(jeton).ConfigureAwait(false);

        var couleur = await _uniteDeTravail.Depot<Couleur>().Requete()
            .Where(c => c.Id == couleurId).Select(c => c.Nom).FirstAsync(jeton).ConfigureAwait(false);

        var abregeCouleur = new string(couleur.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
        var abregeTaille = new string(taille.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        var baseSku = $"{produit.Reference.Trim().ToUpperInvariant()}-{abregeCouleur}-{abregeTaille}";
        var sku = baseSku;
        var suffixe = 1;

        while (await _uniteDeTravail.Variantes.SkuExisteAsync(sku, null, jeton).ConfigureAwait(false))
        {
            sku = $"{baseSku}-{suffixe++}";
        }

        return sku;
    }

    private async Task<ProduitDto> ObtenirProduitObligatoireAsync(int produitId, CancellationToken jeton)
    {
        _uniteDeTravail.ViderSuivi();

        return await ObtenirProduitAsync(produitId, jeton).ConfigureAwait(false)
               ?? throw new EntiteIntrouvableException("Produit", produitId);
    }

    private async Task<VarianteDto> ObtenirVarianteObligatoireAsync(int varianteId, CancellationToken jeton)
    {
        _uniteDeTravail.ViderSuivi();

        var variante = await _uniteDeTravail.Variantes.ObtenirCompleteAsync(varianteId, jeton).ConfigureAwait(false)
                       ?? throw new EntiteIntrouvableException("Déclinaison", varianteId);

        return ProjeterVariante(variante);
    }

    private static ProduitDto Projeter(Produit produit) => new()
    {
        Id = produit.Id,
        Reference = produit.Reference,
        Sku = produit.SKU,
        Nom = produit.Nom,
        Description = produit.Description,
        MarqueId = produit.MarqueId,
        Marque = produit.Marque?.Nom,
        CategorieId = produit.CategorieId,
        Categorie = produit.Categorie?.Nom ?? string.Empty,
        SystemeTailleId = produit.Categorie?.SystemeTailleId ?? 0,
        SystemeTaille = produit.Categorie?.SystemeTaille?.Nom ?? string.Empty,
        Collection = produit.Collection,
        Saison = produit.Saison,
        PrixAchat = produit.PrixAchat,
        PrixVente = produit.PrixVente,
        Photo = produit.Photo,
        Actif = produit.Actif,
        DateCreation = produit.DateCreation,
        DateModification = produit.DateModification,
        Variantes = produit.Variantes
            .OrderBy(v => v.Couleur?.Nom)
            .ThenBy(v => v.Taille?.Ordre)
            .Select(v => ProjeterVariante(v, produit))
            .ToList()
    };

    private static VarianteDto ProjeterVariante(VarianteProduit variante, Produit? produit = null)
    {
        var reference = produit ?? variante.Produit;

        return new VarianteDto
        {
            Id = variante.Id,
            ProduitId = variante.ProduitId,
            ProduitNom = reference?.Nom ?? string.Empty,
            Reference = reference?.Reference ?? string.Empty,
            Marque = reference?.Marque?.Nom,
            TailleId = variante.TailleId,
            Taille = variante.Taille?.Nom ?? string.Empty,
            OrdreTaille = variante.Taille?.Ordre ?? 0,
            CouleurId = variante.CouleurId,
            Couleur = variante.Couleur?.Nom ?? string.Empty,
            CodeCouleur = variante.Couleur?.CodeCouleur,
            Sku = variante.SKU,
            CodeBarres = variante.CodeBarres,
            PrixAchatSpecifique = variante.PrixAchat,
            PrixVenteSpecifique = variante.PrixVente,
            PrixAchat = variante.PrixAchat ?? reference?.PrixAchat ?? 0m,
            PrixVente = variante.PrixVente ?? reference?.PrixVente ?? 0m,
            SeuilMinimum = variante.SeuilMinimum,
            QuantiteDisponible = variante.Inventaire?.QuantiteDisponible ?? 0,
            Actif = variante.Actif
        };
    }
}
