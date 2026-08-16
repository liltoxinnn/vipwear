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

/// <inheritdoc cref="IServiceAchats"/>
public class ServiceAchats : IServiceAchats
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IServiceStock _stock;
    private readonly IServiceAudit _audit;
    private readonly ISessionUtilisateur _session;
    private readonly IGenerateurNumeros _numeros;
    private readonly IFournisseurHorodatage _horodatage;
    private readonly ILogger<ServiceAchats> _journal;

    public ServiceAchats(
        IUniteDeTravail uniteDeTravail,
        IServiceStock stock,
        IServiceAudit audit,
        ISessionUtilisateur session,
        IGenerateurNumeros numeros,
        IFournisseurHorodatage horodatage,
        ILogger<ServiceAchats> journal)
    {
        _uniteDeTravail = uniteDeTravail;
        _stock = stock;
        _audit = audit;
        _session = session;
        _numeros = numeros;
        _horodatage = horodatage;
        _journal = journal;
    }

    public async Task<AchatDto> CreerAchatAsync(DemandeAchat demande, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererAchats);

        if (demande.Lignes.Count == 0)
        {
            throw new ValidationMetierException("La commande ne contient aucun article.");
        }

        if (demande.Lignes.Any(l => l.Quantite <= 0))
        {
            throw new ValidationMetierException("La quantité de chaque article doit être supérieure à zéro.");
        }

        if (demande.Remise < 0 || demande.MontantPaye < 0)
        {
            throw new ValidationMetierException("Les montants d'une commande ne peuvent pas être négatifs.");
        }

        var utilisateurId = _session.UtilisateurIdObligatoire;

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var fournisseur = await _uniteDeTravail.Depot<Fournisseur>().Requete()
            .FirstOrDefaultAsync(f => f.Id == demande.FournisseurId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Fournisseur", demande.FournisseurId);

        if (!fournisseur.Actif)
        {
            throw new ValidationMetierException(
                $"Le fournisseur « {fournisseur.Nom} » est désactivé. Réactivez sa fiche pour commander.");
        }

        var variantes = await ChargerVariantesAsync(
            demande.Lignes.Select(l => l.VarianteProduitId), jeton).ConfigureAwait(false);

        var numeroAchat = await _numeros.ProchainNumeroAchatAsync(jeton).ConfigureAwait(false);

        var achat = new Achat
        {
            NumeroAchat = numeroAchat,
            FournisseurId = demande.FournisseurId,
            UtilisateurId = utilisateurId,
            DateAchat = _horodatage.MaintenantUtc,
            Statut = StatutAchat.EnAttente,
            Notes = demande.Notes
        };

        decimal sousTotal = 0m;

        foreach (var ligne in demande.Lignes)
        {
            var variante = variantes[ligne.VarianteProduitId];
            var prixUnitaire = ligne.PrixUnitaire ?? variante.PrixAchatEffectif;

            if (prixUnitaire < 0)
            {
                throw new ValidationMetierException("Le prix d'achat ne peut pas être négatif.");
            }

            var totalLigne = prixUnitaire * ligne.Quantite;

            achat.Lignes.Add(new LigneAchat
            {
                VarianteProduitId = ligne.VarianteProduitId,
                Quantite = ligne.Quantite,
                QuantiteRecue = 0,
                PrixUnitaire = prixUnitaire,
                Total = totalLigne
            });

            sousTotal += totalLigne;
        }

        if (demande.Remise > sousTotal)
        {
            throw new ValidationMetierException("La remise dépasse le montant total de la commande.");
        }

        achat.SousTotal = sousTotal;
        achat.Remise = demande.Remise;
        achat.Total = sousTotal - demande.Remise;

        if (demande.MontantPaye > achat.Total)
        {
            throw new ValidationMetierException(
                $"Le montant versé dépasse le total de la commande ({achat.Total:N2} DA).");
        }

        achat.MontantPaye = demande.MontantPaye;
        achat.MontantRestant = achat.Total - demande.MontantPaye;

        await _uniteDeTravail.Depot<Achat>().AjouterAsync(achat, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        await _audit.TracerAsync(
            ActionsAudit.Achat,
            nameof(Achat),
            achat.Id,
            $"Commande {numeroAchat} créée auprès de {fournisseur.Nom} " +
            $"pour {achat.Total:N2} DA ({achat.Lignes.Sum(l => l.Quantite)} article(s)).",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        if (demande.ReceptionImmediate)
        {
            await ReceptionnerLignesAsync(achat.Id, null, jeton).ConfigureAwait(false);
        }

        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation("Commande fournisseur {Numero} créée.", numeroAchat);

        return await ObtenirObligatoireAsync(achat.Id, jeton).ConfigureAwait(false);
    }

    public async Task<AchatDto> ReceptionnerAsync(
        int achatId,
        IReadOnlyList<LigneReception> receptions,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererAchats);

        if (receptions.Count == 0)
        {
            throw new ValidationMetierException("Aucune quantité à réceptionner n'a été saisie.");
        }

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        await ReceptionnerLignesAsync(achatId, receptions, jeton).ConfigureAwait(false);

        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(achatId, jeton).ConfigureAwait(false);
    }

    public async Task<AchatDto> ReceptionnerToutAsync(int achatId, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererAchats);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        await ReceptionnerLignesAsync(achatId, null, jeton).ConfigureAwait(false);

        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(achatId, jeton).ConfigureAwait(false);
    }

    /// <summary>
    /// Applique une réception. <paramref name="receptions"/> à null signifie
    /// « recevoir tout ce qui reste ». Chaque quantité reçue augmente le stock
    /// via le service centralisé, qui crée le mouvement correspondant.
    /// </summary>
    private async Task ReceptionnerLignesAsync(
        int achatId,
        IReadOnlyList<LigneReception>? receptions,
        CancellationToken jeton)
    {
        var achat = await _uniteDeTravail.Depot<Achat>().RequeteSuivie()
            .Include(a => a.Lignes)
            .FirstOrDefaultAsync(a => a.Id == achatId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Commande fournisseur", achatId);

        if (achat.Statut == StatutAchat.Annule)
        {
            throw new ValidationMetierException("Cette commande est annulée : elle ne peut plus être réceptionnée.");
        }

        if (achat.Statut == StatutAchat.Recu)
        {
            throw new ValidationMetierException("Cette commande a déjà été entièrement réceptionnée.");
        }

        var aTraiter = new List<(LigneAchat Ligne, int Quantite)>();

        if (receptions is null)
        {
            aTraiter.AddRange(achat.Lignes
                .Where(l => l.QuantiteRestante > 0)
                .Select(l => (l, l.QuantiteRestante)));
        }
        else
        {
            foreach (var reception in receptions.Where(r => r.QuantiteRecue != 0))
            {
                var ligne = achat.Lignes.FirstOrDefault(l => l.Id == reception.LigneAchatId)
                            ?? throw new EntiteIntrouvableException("Ligne de commande", reception.LigneAchatId);

                if (reception.QuantiteRecue < 0)
                {
                    throw new ValidationMetierException("Une quantité reçue ne peut pas être négative.");
                }

                if (reception.QuantiteRecue > ligne.QuantiteRestante)
                {
                    throw new ValidationMetierException(
                        $"La quantité reçue ({reception.QuantiteRecue}) dépasse la quantité restante " +
                        $"à recevoir ({ligne.QuantiteRestante}) pour cet article.");
                }

                aTraiter.Add((ligne, reception.QuantiteRecue));
            }
        }

        if (aTraiter.Count == 0)
        {
            throw new ValidationMetierException("Il n'y a plus aucun article à réceptionner sur cette commande.");
        }

        // Ordre stable sur l'identifiant de déclinaison pour éviter tout
        // blocage entre deux réceptions simultanées.
        foreach (var (ligne, quantite) in aTraiter.OrderBy(t => t.Ligne.VarianteProduitId))
        {
            await _stock.AjouterStockAsync(
                ligne.VarianteProduitId,
                quantite,
                TypeMouvementStock.Achat,
                $"Réception de la commande {achat.NumeroAchat}",
                achat.NumeroAchat,
                jeton).ConfigureAwait(false);

            ligne.QuantiteRecue += quantite;
        }

        var totalCommande = achat.Lignes.Sum(l => l.Quantite);
        var totalRecu = achat.Lignes.Sum(l => l.QuantiteRecue);

        if (totalRecu >= totalCommande)
        {
            achat.Statut = StatutAchat.Recu;
            achat.DateReception = _horodatage.MaintenantUtc;
        }
        else
        {
            achat.Statut = StatutAchat.PartiellementRecu;
        }

        await _audit.TracerAsync(
            ActionsAudit.Reception,
            nameof(Achat),
            achat.Id,
            $"Réception sur la commande {achat.NumeroAchat} : " +
            $"{aTraiter.Sum(t => t.Quantite)} article(s) intégré(s) au stock " +
            $"({totalRecu}/{totalCommande} au total).",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation(
            "Réception sur la commande {Numero} : {Recu}/{Commande} articles.",
            achat.NumeroAchat, totalRecu, totalCommande);
    }

    public async Task<AchatDto> EnregistrerReglementAsync(
        int achatId,
        decimal montant,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererAchats);

        if (montant <= 0)
        {
            throw new ValidationMetierException("Le montant du règlement doit être supérieur à zéro.");
        }

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var achat = await _uniteDeTravail.Depot<Achat>().RequeteSuivie()
            .FirstOrDefaultAsync(a => a.Id == achatId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Commande fournisseur", achatId);

        if (achat.Statut == StatutAchat.Annule)
        {
            throw new ValidationMetierException("Cette commande est annulée : aucun règlement ne peut y être ajouté.");
        }

        if (montant > achat.MontantRestant)
        {
            throw new ValidationMetierException(
                $"Le montant saisi dépasse le reste à payer ({achat.MontantRestant:N2} DA).");
        }

        achat.MontantPaye += montant;
        achat.MontantRestant = achat.Total - achat.MontantPaye;

        await _audit.TracerAsync(
            ActionsAudit.Achat,
            nameof(Achat),
            achat.Id,
            $"Règlement de {montant:N2} DA sur la commande {achat.NumeroAchat}. " +
            $"Reste à payer : {achat.MontantRestant:N2} DA.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(achatId, jeton).ConfigureAwait(false);
    }

    public async Task<AchatDto> AnnulerAchatAsync(int achatId, string motif, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererAchats);

        if (string.IsNullOrWhiteSpace(motif))
        {
            throw new ValidationMetierException("Veuillez indiquer le motif de l'annulation.");
        }

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var achat = await _uniteDeTravail.Depot<Achat>().RequeteSuivie()
            .Include(a => a.Lignes)
            .FirstOrDefaultAsync(a => a.Id == achatId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Commande fournisseur", achatId);

        if (achat.Statut == StatutAchat.Annule)
        {
            throw new ValidationMetierException("Cette commande est déjà annulée.");
        }

        if (achat.Lignes.Any(l => l.QuantiteRecue > 0))
        {
            throw new ValidationMetierException(
                "Cette commande ne peut pas être annulée car une partie de la marchandise a déjà " +
                "rejoint le stock. Enregistrez un retour fournisseur à la place.");
        }

        achat.Statut = StatutAchat.Annule;
        achat.Notes = string.IsNullOrWhiteSpace(achat.Notes)
            ? $"Annulée : {motif}"
            : $"{achat.Notes}{Environment.NewLine}Annulée : {motif}";

        await _audit.TracerAsync(
            ActionsAudit.Achat,
            nameof(Achat),
            achat.Id,
            $"Commande {achat.NumeroAchat} annulée. Motif : {motif}",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(achatId, jeton).ConfigureAwait(false);
    }

    public async Task<AchatDto?> ObtenirAsync(int achatId, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirAchats);

        var achat = await _uniteDeTravail.Depot<Achat>().Requete()
            .Include(a => a.Fournisseur)
            .Include(a => a.Utilisateur)
            .Include(a => a.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(v => v.Produit)
            .Include(a => a.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(v => v.Couleur)
            .Include(a => a.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(v => v.Taille)
            .FirstOrDefaultAsync(a => a.Id == achatId, jeton)
            .ConfigureAwait(false);

        return achat is null ? null : Projeter(achat);
    }

    public async Task<IReadOnlyList<ResumeAchatDto>> RechercherAsync(
        DateTime? debut = null,
        DateTime? fin = null,
        int? fournisseurId = null,
        StatutAchat? statut = null,
        string? recherche = null,
        int limite = 500,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirAchats);

        var requete = _uniteDeTravail.Depot<Achat>().Requete();

        if (debut.HasValue)
        {
            requete = requete.Where(a => a.DateAchat >= debut.Value);
        }

        if (fin.HasValue)
        {
            requete = requete.Where(a => a.DateAchat <= fin.Value);
        }

        if (fournisseurId.HasValue)
        {
            requete = requete.Where(a => a.FournisseurId == fournisseurId.Value);
        }

        if (statut.HasValue)
        {
            requete = requete.Where(a => a.Statut == statut.Value);
        }

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var terme = recherche.Trim().ToLowerInvariant();

            requete = requete.Where(a =>
                a.NumeroAchat.ToLower().Contains(terme)
                || a.Fournisseur.Nom.ToLower().Contains(terme));
        }

        return await requete
            .OrderByDescending(a => a.DateAchat)
            .ThenByDescending(a => a.Id)
            .Take(limite)
            .Select(a => new ResumeAchatDto
            {
                Id = a.Id,
                NumeroAchat = a.NumeroAchat,
                DateAchat = a.DateAchat,
                FournisseurNom = a.Fournisseur.Nom,
                NombreArticles = a.Lignes.Sum(l => (int?)l.Quantite) ?? 0,
                Total = a.Total,
                MontantPaye = a.MontantPaye,
                MontantRestant = a.MontantRestant,
                Statut = a.Statut,
                LibelleStatut = LibellesEnums.Libelle(a.Statut)
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    // ------------------------------------------------------------------
    // Utilitaires internes
    // ------------------------------------------------------------------

    private async Task<AchatDto> ObtenirObligatoireAsync(int achatId, CancellationToken jeton)
    {
        _uniteDeTravail.ViderSuivi();

        return await ObtenirAsync(achatId, jeton).ConfigureAwait(false)
               ?? throw new EntiteIntrouvableException("Commande fournisseur", achatId);
    }

    private static AchatDto Projeter(Achat achat) => new()
    {
        Id = achat.Id,
        NumeroAchat = achat.NumeroAchat,
        FournisseurId = achat.FournisseurId,
        FournisseurNom = achat.Fournisseur?.Nom ?? string.Empty,
        FournisseurTelephone = achat.Fournisseur?.Telephone,
        UtilisateurNom = achat.Utilisateur?.NomComplet ?? string.Empty,
        DateAchat = achat.DateAchat,
        DateReception = achat.DateReception,
        SousTotal = achat.SousTotal,
        Remise = achat.Remise,
        Total = achat.Total,
        MontantPaye = achat.MontantPaye,
        MontantRestant = achat.MontantRestant,
        Statut = achat.Statut,
        LibelleStatut = achat.Statut.Libelle(),
        Notes = achat.Notes,
        Lignes = achat.Lignes.Select(l => new LigneAchatDto
        {
            Id = l.Id,
            VarianteProduitId = l.VarianteProduitId,
            Designation = l.VarianteProduit.Designation,
            Sku = l.VarianteProduit.SKU,
            Couleur = l.VarianteProduit.Couleur?.Nom ?? string.Empty,
            Taille = l.VarianteProduit.Taille?.Nom ?? string.Empty,
            Quantite = l.Quantite,
            QuantiteRecue = l.QuantiteRecue,
            PrixUnitaire = l.PrixUnitaire,
            Total = l.Total
        }).ToList()
    };

    private async Task<Dictionary<int, VarianteProduit>> ChargerVariantesAsync(
        IEnumerable<int> identifiants,
        CancellationToken jeton)
    {
        var ids = identifiants.Distinct().ToArray();

        var variantes = await _uniteDeTravail.Variantes.Requete()
            .Include(v => v.Produit)
            .Include(v => v.Couleur)
            .Include(v => v.Taille)
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, jeton)
            .ConfigureAwait(false);

        if (variantes.Count != ids.Length)
        {
            throw new EntiteIntrouvableException(
                "Un article de la commande n'existe plus dans le catalogue.");
        }

        return variantes;
    }
}
