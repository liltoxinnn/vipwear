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

/// <inheritdoc cref="IServiceRetours"/>
public class ServiceRetours : IServiceRetours
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IServiceStock _stock;
    private readonly IServiceVentes _ventes;
    private readonly IServiceAudit _audit;
    private readonly ISessionUtilisateur _session;
    private readonly IGenerateurNumeros _numeros;
    private readonly IFournisseurHorodatage _horodatage;
    private readonly ILogger<ServiceRetours> _journal;

    public ServiceRetours(
        IUniteDeTravail uniteDeTravail,
        IServiceStock stock,
        IServiceVentes ventes,
        IServiceAudit audit,
        ISessionUtilisateur session,
        IGenerateurNumeros numeros,
        IFournisseurHorodatage horodatage,
        ILogger<ServiceRetours> journal)
    {
        _uniteDeTravail = uniteDeTravail;
        _stock = stock;
        _ventes = ventes;
        _audit = audit;
        _session = session;
        _numeros = numeros;
        _horodatage = horodatage;
        _journal = journal;
    }

    public async Task<RetourDto> EnregistrerRetourAsync(DemandeRetour demande, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.EffectuerRetour);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var retourId = await CreerRetourAsync(demande, jeton).ConfigureAwait(false);

        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(retourId, jeton).ConfigureAwait(false);
    }

    public async Task<ResultatEchangeDto> EnregistrerEchangeAsync(
        DemandeEchange demande,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.EffectuerRetour);
        _session.ExigerPermission(CodesPermissions.EffectuerVente);

        if (demande.LignesRetournees.Count == 0)
        {
            throw new ValidationMetierException("Aucun article rapporté n'a été saisi pour cet échange.");
        }

        if (demande.LignesNouvelles.Count == 0)
        {
            throw new ValidationMetierException("Aucun article de remplacement n'a été saisi pour cet échange.");
        }

        // Le retour et la nouvelle vente partagent la même transaction :
        // il ne peut jamais rester un retour enregistré sans son remplacement,
        // ni l'inverse.
        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var venteOrigine = await _uniteDeTravail.Depot<Vente>().Requete()
            .FirstOrDefaultAsync(v => v.Id == demande.VenteId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Vente", demande.VenteId);

        var retourId = await CreerRetourAsync(
            new DemandeRetour
            {
                VenteId = demande.VenteId,
                Motif = demande.Motif ?? "Échange d'articles",
                Lignes = demande.LignesRetournees
            },
            jeton).ConfigureAwait(false);

        var retour = await _uniteDeTravail.Depot<Retour>().Requete()
            .FirstAsync(r => r.Id == retourId, jeton)
            .ConfigureAwait(false);

        var nouvelleVente = await _ventes.EnregistrerVenteAsync(
            new DemandeVente
            {
                ClientId = venteOrigine.ClientId,
                Lignes = demande.LignesNouvelles,
                Paiements = demande.Paiements,
                Notes = $"Échange lié au retour {retour.NumeroRetour} (vente {venteOrigine.NumeroVente})."
            },
            jeton).ConfigureAwait(false);

        var solde = nouvelleVente.Total - retour.TotalRembourse;

        await _audit.TracerAsync(
            ActionsAudit.Retour,
            nameof(Retour),
            retour.Id,
            $"Échange sur la vente {venteOrigine.NumeroVente} : retour {retour.NumeroRetour} " +
            $"({retour.TotalRembourse:N2} DA) contre la vente {nouvelleVente.NumeroVente} " +
            $"({nouvelleVente.Total:N2} DA). Solde : {solde:N2} DA.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation(
            "Échange enregistré : retour {Retour} contre vente {Vente}, solde {Solde} DA.",
            retour.NumeroRetour, nouvelleVente.NumeroVente, solde);

        return new ResultatEchangeDto
        {
            Retour = await ObtenirObligatoireAsync(retourId, jeton).ConfigureAwait(false),
            NouvelleVente = nouvelleVente,
            SoldeAPayer = solde
        };
    }

    /// <summary>
    /// Cœur du traitement d'un retour, exécuté dans la transaction ouverte par
    /// l'appelant. Retourne l'identifiant du retour créé.
    /// </summary>
    private async Task<int> CreerRetourAsync(DemandeRetour demande, CancellationToken jeton)
    {
        if (demande.Lignes.Count == 0)
        {
            throw new ValidationMetierException("Aucun article à retourner n'a été sélectionné.");
        }

        if (demande.Lignes.Any(l => l.Quantite <= 0))
        {
            throw new ValidationMetierException("La quantité retournée doit être supérieure à zéro.");
        }

        var utilisateurId = _session.UtilisateurIdObligatoire;

        var vente = await _uniteDeTravail.Depot<Vente>().RequeteSuivie()
            .Include(v => v.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(v => v.Produit)
            .Include(v => v.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(v => v.Couleur)
            .Include(v => v.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(v => v.Taille)
            .FirstOrDefaultAsync(v => v.Id == demande.VenteId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Vente", demande.VenteId);

        if (vente.Statut == StatutVente.Annulee)
        {
            throw new ValidationMetierException(
                "Cette vente est annulée : les articles ont déjà été réintégrés au stock.");
        }

        await ControlerDelaiRetourAsync(vente, jeton).ConfigureAwait(false);

        // Le remboursement tient compte de la remise appliquée au ticket :
        // le client est remboursé de ce qu'il a réellement payé, pas du prix
        // catalogue.
        var facteurRemiseTicket = vente.SousTotal > 0 ? vente.Total / vente.SousTotal : 1m;

        var numeroRetour = await _numeros.ProchainNumeroRetourAsync(jeton).ConfigureAwait(false);

        var retour = new Retour
        {
            NumeroRetour = numeroRetour,
            VenteId = vente.Id,
            ClientId = vente.ClientId,
            UtilisateurId = utilisateurId,
            DateRetour = _horodatage.MaintenantUtc,
            Motif = demande.Motif,
            Statut = StatutRetour.Valide
        };

        decimal totalRembourse = 0m;
        var aRemettreEnStock = new List<(int VarianteProduitId, int Quantite)>();

        // Les demandes portant sur une même ligne de vente sont regroupées
        // pour que le contrôle de quantité porte sur le total demandé.
        foreach (var groupe in demande.Lignes.GroupBy(l => l.LigneVenteId))
        {
            var ligneVente = vente.Lignes.FirstOrDefault(l => l.Id == groupe.Key)
                             ?? throw new ValidationMetierException(
                                 "Un des articles sélectionnés n'appartient pas à cette vente.");

            var quantiteDemandee = groupe.Sum(l => l.Quantite);

            if (quantiteDemandee > ligneVente.QuantiteRetournable)
            {
                throw new ValidationMetierException(
                    $"Impossible de reprendre {quantiteDemandee} × « {ligneVente.VarianteProduit.Designation} » : " +
                    $"seulement {ligneVente.QuantiteRetournable} article(s) peuvent encore être retournés " +
                    $"sur cette vente.");
            }

            // Montant réellement payé pour un exemplaire de cet article.
            var montantUnitairePaye = ligneVente.Quantite > 0
                ? (ligneVente.Total / ligneVente.Quantite) * facteurRemiseTicket
                : 0m;

            foreach (var ligneDemandee in groupe)
            {
                var montantLigne = Math.Round(montantUnitairePaye * ligneDemandee.Quantite, 2);
                var revendable = ligneDemandee.EtatArticle == EtatArticleRetour.Revendable;

                retour.Lignes.Add(new LigneRetour
                {
                    LigneVenteId = ligneVente.Id,
                    VarianteProduitId = ligneVente.VarianteProduitId,
                    Quantite = ligneDemandee.Quantite,
                    EtatArticle = ligneDemandee.EtatArticle,
                    // Un article endommagé est repris et remboursé, mais il ne
                    // rejoint jamais le stock vendable.
                    RemisEnStock = revendable,
                    MontantRembourse = montantLigne
                });

                if (revendable)
                {
                    aRemettreEnStock.Add((ligneVente.VarianteProduitId, ligneDemandee.Quantite));
                }

                totalRembourse += montantLigne;
            }

            ligneVente.QuantiteRetournee += quantiteDemandee;
        }

        retour.TotalRembourse = totalRembourse;

        await _uniteDeTravail.Depot<Retour>().AjouterAsync(retour, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        foreach (var element in aRemettreEnStock
                     .GroupBy(e => e.VarianteProduitId)
                     .Select(g => new { VarianteProduitId = g.Key, Quantite = g.Sum(e => e.Quantite) })
                     .OrderBy(e => e.VarianteProduitId))
        {
            await _stock.RetournerStockAsync(
                element.VarianteProduitId,
                element.Quantite,
                $"Retour {numeroRetour} sur la vente {vente.NumeroVente}",
                numeroRetour,
                jeton).ConfigureAwait(false);
        }

        MettreAJourStatutVente(vente);

        var nombreEndommages = retour.Lignes
            .Where(l => l.EtatArticle == EtatArticleRetour.Endommage)
            .Sum(l => l.Quantite);

        var description =
            $"Retour {numeroRetour} sur la vente {vente.NumeroVente} : " +
            $"{retour.Lignes.Sum(l => l.Quantite)} article(s) repris, " +
            $"{totalRembourse:N2} DA remboursés.";

        if (nombreEndommages > 0)
        {
            description += $" {nombreEndommages} article(s) endommagé(s) non réintégré(s) au stock.";
        }

        await _audit.TracerAsync(
            ActionsAudit.Retour,
            nameof(Retour),
            retour.Id,
            description,
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation(
            "Retour {Numero} enregistré sur la vente {Vente} : {Montant} DA remboursés.",
            numeroRetour, vente.NumeroVente, totalRembourse);

        return retour.Id;
    }

    /// <summary>
    /// Fait évoluer le statut de la vente selon ce qu'il reste chez le client.
    /// </summary>
    private static void MettreAJourStatutVente(Vente vente)
    {
        var totalVendu = vente.Lignes.Sum(l => l.Quantite);
        var totalRetourne = vente.Lignes.Sum(l => l.QuantiteRetournee);

        if (totalRetourne >= totalVendu)
        {
            vente.Statut = StatutVente.Retournee;
        }
        else if (totalRetourne > 0)
        {
            vente.Statut = StatutVente.PartiellementRetournee;
        }
    }

    /// <summary>
    /// Vérifie que la vente est encore dans le délai de retour fixé par le
    /// magasin. Un délai à zéro désactive le contrôle.
    /// </summary>
    private async Task ControlerDelaiRetourAsync(Vente vente, CancellationToken jeton)
    {
        var delai = await _uniteDeTravail.Depot<ParametresMagasin>().Requete()
            .Select(p => p.DelaiRetourJours)
            .FirstOrDefaultAsync(jeton)
            .ConfigureAwait(false);

        if (delai <= 0)
        {
            return;
        }

        var limite = vente.DateVente.AddDays(delai);

        if (_horodatage.MaintenantUtc > limite)
        {
            throw new ValidationMetierException(
                $"Le délai de retour de {delai} jours est dépassé pour la vente {vente.NumeroVente} " +
                $"(vendue le {vente.DateVente.ToLocalTime():dd/MM/yyyy}).");
        }
    }

    public async Task<RetourDto?> ObtenirAsync(int retourId, CancellationToken jeton = default)
    {
        var retour = await _uniteDeTravail.Depot<Retour>().Requete()
            .Include(r => r.Vente)
            .Include(r => r.Client)
            .Include(r => r.Utilisateur)
            .Include(r => r.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(v => v.Produit)
            .Include(r => r.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(v => v.Couleur)
            .Include(r => r.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(v => v.Taille)
            .FirstOrDefaultAsync(r => r.Id == retourId, jeton)
            .ConfigureAwait(false);

        return retour is null ? null : Projeter(retour);
    }

    public async Task<IReadOnlyList<ResumeRetourDto>> RechercherAsync(
        DateTime? debut = null,
        DateTime? fin = null,
        int? clientId = null,
        string? recherche = null,
        int limite = 500,
        CancellationToken jeton = default)
    {
        var requete = _uniteDeTravail.Depot<Retour>().Requete();

        if (debut.HasValue)
        {
            requete = requete.Where(r => r.DateRetour >= debut.Value);
        }

        if (fin.HasValue)
        {
            requete = requete.Where(r => r.DateRetour <= fin.Value);
        }

        if (clientId.HasValue)
        {
            requete = requete.Where(r => r.ClientId == clientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var terme = recherche.Trim().ToLowerInvariant();

            requete = requete.Where(r =>
                r.NumeroRetour.ToLower().Contains(terme)
                || r.Vente.NumeroVente.ToLower().Contains(terme));
        }

        return await requete
            .OrderByDescending(r => r.DateRetour)
            .ThenByDescending(r => r.Id)
            .Take(limite)
            .Select(r => new ResumeRetourDto
            {
                Id = r.Id,
                NumeroRetour = r.NumeroRetour,
                NumeroVente = r.Vente.NumeroVente,
                DateRetour = r.DateRetour,
                ClientNom = r.Client != null ? r.Client.Prenom + " " + r.Client.Nom : null,
                UtilisateurNom = r.Utilisateur.Prenom + " " + r.Utilisateur.Nom,
                NombreArticles = r.Lignes.Sum(l => (int?)l.Quantite) ?? 0,
                TotalRembourse = r.TotalRembourse,
                Motif = r.Motif,
                Statut = r.Statut,
                LibelleStatut = LibellesEnums.Libelle(r.Statut)
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    private async Task<RetourDto> ObtenirObligatoireAsync(int retourId, CancellationToken jeton)
    {
        _uniteDeTravail.ViderSuivi();

        return await ObtenirAsync(retourId, jeton).ConfigureAwait(false)
               ?? throw new EntiteIntrouvableException("Retour", retourId);
    }

    private static RetourDto Projeter(Retour retour) => new()
    {
        Id = retour.Id,
        NumeroRetour = retour.NumeroRetour,
        VenteId = retour.VenteId,
        NumeroVente = retour.Vente?.NumeroVente ?? string.Empty,
        DateVente = retour.Vente?.DateVente ?? default,
        ClientId = retour.ClientId,
        ClientNom = retour.Client?.NomComplet,
        UtilisateurNom = retour.Utilisateur?.NomComplet ?? string.Empty,
        DateRetour = retour.DateRetour,
        Motif = retour.Motif,
        TotalRembourse = retour.TotalRembourse,
        Statut = retour.Statut,
        LibelleStatut = retour.Statut.Libelle(),
        Lignes = retour.Lignes.Select(l => new LigneRetourDto
        {
            Id = l.Id,
            VarianteProduitId = l.VarianteProduitId,
            Designation = l.VarianteProduit.Designation,
            Sku = l.VarianteProduit.SKU,
            Quantite = l.Quantite,
            EtatArticle = l.EtatArticle,
            LibelleEtatArticle = l.EtatArticle.Libelle(),
            RemisEnStock = l.RemisEnStock,
            MontantRembourse = l.MontantRembourse
        }).ToList()
    };
}
