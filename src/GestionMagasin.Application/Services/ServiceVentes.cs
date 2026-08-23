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

/// <inheritdoc cref="IServiceVentes"/>
public class ServiceVentes : IServiceVentes
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IServiceStock _stock;
    private readonly IServiceAudit _audit;
    private readonly ISessionUtilisateur _session;
    private readonly IGenerateurNumeros _numeros;
    private readonly IFournisseurHorodatage _horodatage;
    private readonly ILogger<ServiceVentes> _journal;

    public ServiceVentes(
        IUniteDeTravail uniteDeTravail,
        IServiceStock stock,
        IServiceAudit audit,
        ISessionUtilisateur session,
        IGenerateurNumeros numeros,
        IFournisseurHorodatage horodatage,
        ILogger<ServiceVentes> journal)
    {
        _uniteDeTravail = uniteDeTravail;
        _stock = stock;
        _audit = audit;
        _session = session;
        _numeros = numeros;
        _horodatage = horodatage;
        _journal = journal;
    }

    public async Task<VenteDto> EnregistrerVenteAsync(DemandeVente demande, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.EffectuerVente);

        var lignesDemandees = PreparerLignes(demande);

        // Une remise, de ligne ou de ticket, demande une autorisation
        // spécifique : un caissier ne l'accorde pas de sa propre initiative.
        if (demande.RemiseGlobale > 0 || lignesDemandees.Any(l => l.Remise > 0))
        {
            _session.ExigerPermission(CodesPermissions.AccorderRemise);
        }

        var utilisateurId = _session.UtilisateurIdObligatoire;

        _journal.LogDebug("Vente : ouverture de la transaction.");

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        _journal.LogDebug("Vente : chargement des déclinaisons.");

        var variantes = await ChargerVariantesAsync(lignesDemandees.Select(l => l.VarianteProduitId), jeton)
            .ConfigureAwait(false);

        await ControlerClientAsync(demande.ClientId, jeton).ConfigureAwait(false);

        var maintenant = _horodatage.MaintenantUtc;
        _journal.LogDebug("Vente : attribution du numéro.");

        var numeroVente = await _numeros.ProchainNumeroVenteAsync(jeton).ConfigureAwait(false);

        var vente = new Vente
        {
            NumeroVente = numeroVente,
            ClientId = demande.ClientId,
            UtilisateurId = utilisateurId,
            DateVente = maintenant,
            Statut = StatutVente.Payee,
            Notes = demande.Notes
        };

        decimal sousTotal = 0m;

        foreach (var ligneDemandee in lignesDemandees)
        {
            var variante = variantes[ligneDemandee.VarianteProduitId];

            var prixUnitaire = ligneDemandee.PrixUnitaire ?? variante.PrixVenteEffectif;

            if (prixUnitaire < 0)
            {
                throw new ValidationMetierException("Le prix de vente ne peut pas être négatif.");
            }

            var totalLigne = (prixUnitaire * ligneDemandee.Quantite) - ligneDemandee.Remise;

            if (totalLigne < 0)
            {
                throw new ValidationMetierException(
                    $"La remise appliquée à « {variante.Designation} » dépasse le prix de l'article.");
            }

            vente.Lignes.Add(new LigneVente
            {
                VarianteProduitId = ligneDemandee.VarianteProduitId,
                Quantite = ligneDemandee.Quantite,
                PrixUnitaire = prixUnitaire,
                Remise = ligneDemandee.Remise,
                Total = totalLigne,
                // Le prix d'achat est figé ici : la marge du ticket restera
                // exacte même si le tarif fournisseur change plus tard.
                PrixAchatUnitaire = variante.PrixAchatEffectif,
                QuantiteRetournee = 0
            });

            sousTotal += totalLigne;
        }

        if (demande.RemiseGlobale < 0)
        {
            throw new ValidationMetierException("La remise ne peut pas être négative.");
        }

        if (demande.RemiseGlobale > sousTotal)
        {
            throw new ValidationMetierException(
                "La remise accordée dépasse le montant total du ticket.");
        }

        vente.SousTotal = sousTotal;
        vente.Remise = demande.RemiseGlobale;
        vente.Total = sousTotal - demande.RemiseGlobale;

        var totalPaiements = ControlerPaiements(demande.Paiements, vente.Total);

        vente.MontantPaye = totalPaiements;
        vente.Statut = totalPaiements >= vente.Total ? StatutVente.Payee : StatutVente.PartiellementPayee;

        foreach (var paiement in demande.Paiements.Where(p => p.Montant > 0))
        {
            vente.Paiements.Add(new Paiement
            {
                ModePaiement = paiement.ModePaiement,
                Montant = paiement.Montant,
                DatePaiement = maintenant,
                Reference = paiement.Reference
            });
        }

        _journal.LogDebug("Vente {Numero} : écriture de l'en-tête et des lignes.", numeroVente);

        await _uniteDeTravail.Depot<Vente>().AjouterAsync(vente, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        // Les déclinaisons sont traitées dans un ordre stable et les
        // quantités d'un même article sont regroupées : deux caisses qui
        // vendent les mêmes articles ne peuvent pas se bloquer mutuellement,
        // et l'historique reste lisible (un mouvement par article et par vente).
        var quantitesParVariante = lignesDemandees
            .GroupBy(l => l.VarianteProduitId)
            .Select(g => new { VarianteProduitId = g.Key, Quantite = g.Sum(l => l.Quantite) })
            .OrderBy(g => g.VarianteProduitId);

        foreach (var element in quantitesParVariante)
        {
            _journal.LogDebug("Vente {Numero} : retrait de {Quantite} sur la déclinaison {Variante}.",
                numeroVente, element.Quantite, element.VarianteProduitId);

            await _stock.RetirerStockAsync(
                element.VarianteProduitId,
                element.Quantite,
                TypeMouvementStock.Vente,
                $"Vente {numeroVente}",
                numeroVente,
                jeton).ConfigureAwait(false);
        }

        _journal.LogDebug("Vente {Numero} : écriture de la trace d'audit.", numeroVente);

        await _audit.TracerAsync(
            ActionsAudit.Vente,
            nameof(Vente),
            vente.Id,
            $"Vente {numeroVente} enregistrée : {vente.Lignes.Sum(l => l.Quantite)} article(s) " +
            $"pour {vente.Total:N2} DA.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        _journal.LogDebug("Vente {Numero} : validation de la transaction.", numeroVente);

        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation(
            "Vente {Numero} enregistrée pour un total de {Total} DA.", numeroVente, vente.Total);

        _journal.LogDebug("Vente {Numero} : relecture pour affichage.", numeroVente);

        return await ObtenirObligatoireAsync(vente.Id, jeton).ConfigureAwait(false);
    }

    public async Task<VenteDto> AnnulerVenteAsync(int venteId, string motif, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.AnnulerVente);

        if (string.IsNullOrWhiteSpace(motif))
        {
            throw new ValidationMetierException("Veuillez indiquer le motif de l'annulation.");
        }

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var vente = await _uniteDeTravail.Depot<Vente>().RequeteSuivie()
            .Include(v => v.Lignes)
            .FirstOrDefaultAsync(v => v.Id == venteId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Vente", venteId);

        if (vente.Statut == StatutVente.Annulee)
        {
            throw new ValidationMetierException("Cette vente a déjà été annulée.");
        }

        var possedeRetour = await _uniteDeTravail.Depot<Retour>().Requete()
            .AnyAsync(r => r.VenteId == venteId, jeton)
            .ConfigureAwait(false);

        if (possedeRetour)
        {
            throw new ValidationMetierException(
                "Cette vente ne peut pas être annulée car un retour y est déjà rattaché. " +
                "Enregistrez un retour pour les articles restants.");
        }

        // Seuls les articles encore détenus par le magasin sont réintégrés.
        var aReintegrer = vente.Lignes
            .GroupBy(l => l.VarianteProduitId)
            .Select(g => new { VarianteProduitId = g.Key, Quantite = g.Sum(l => l.Quantite - l.QuantiteRetournee) })
            .Where(g => g.Quantite > 0)
            .OrderBy(g => g.VarianteProduitId);

        foreach (var element in aReintegrer)
        {
            await _stock.AjouterStockAsync(
                element.VarianteProduitId,
                element.Quantite,
                TypeMouvementStock.Correction,
                $"Annulation de la vente {vente.NumeroVente} : {motif}",
                vente.NumeroVente,
                jeton).ConfigureAwait(false);
        }

        vente.Statut = StatutVente.Annulee;
        vente.Notes = string.IsNullOrWhiteSpace(vente.Notes)
            ? $"Annulée : {motif}"
            : $"{vente.Notes}{Environment.NewLine}Annulée : {motif}";

        await _audit.TracerAsync(
            ActionsAudit.AnnulationVente,
            nameof(Vente),
            vente.Id,
            $"Vente {vente.NumeroVente} annulée. Motif : {motif}",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        _journal.LogWarning("Vente {Numero} annulée. Motif : {Motif}", vente.NumeroVente, motif);

        return await ObtenirObligatoireAsync(venteId, jeton).ConfigureAwait(false);
    }

    public async Task<VenteDto> AjouterPaiementAsync(
        int venteId,
        PaiementDemande paiement,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.EffectuerVente);

        if (paiement.Montant <= 0)
        {
            throw new ValidationMetierException("Le montant du règlement doit être supérieur à zéro.");
        }

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var vente = await _uniteDeTravail.Depot<Vente>().RequeteSuivie()
            .FirstOrDefaultAsync(v => v.Id == venteId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Vente", venteId);

        if (vente.Statut == StatutVente.Annulee)
        {
            throw new ValidationMetierException("Cette vente est annulée : aucun règlement ne peut y être ajouté.");
        }

        var restant = vente.Total - vente.MontantPaye;

        if (restant <= 0)
        {
            throw new ValidationMetierException("Cette vente est déjà réglée en totalité.");
        }

        if (paiement.Montant > restant)
        {
            throw new ValidationMetierException(
                $"Le montant saisi dépasse le reste à payer ({restant:N2} DA). " +
                "Saisissez le montant dû et rendez la monnaie au client.");
        }

        await _uniteDeTravail.Depot<Paiement>().AjouterAsync(new Paiement
        {
            VenteId = vente.Id,
            ModePaiement = paiement.ModePaiement,
            Montant = paiement.Montant,
            DatePaiement = _horodatage.MaintenantUtc,
            Reference = paiement.Reference
        }, jeton).ConfigureAwait(false);

        vente.MontantPaye += paiement.Montant;

        if (vente.MontantPaye >= vente.Total && vente.Statut == StatutVente.PartiellementPayee)
        {
            vente.Statut = StatutVente.Payee;
        }

        await _audit.TracerAsync(
            ActionsAudit.Vente,
            nameof(Vente),
            vente.Id,
            $"Règlement de {paiement.Montant:N2} DA ({paiement.ModePaiement.Libelle()}) " +
            $"sur la vente {vente.NumeroVente}.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(venteId, jeton).ConfigureAwait(false);
    }

    public async Task<VenteDto?> ObtenirAsync(int venteId, CancellationToken jeton = default)
    {
        var vente = await ConstruireRequeteDetail()
            .FirstOrDefaultAsync(v => v.Id == venteId, jeton)
            .ConfigureAwait(false);

        return vente is null ? null : Projeter(vente);
    }

    public async Task<VenteDto?> ObtenirParNumeroAsync(string numeroVente, CancellationToken jeton = default)
    {
        var recherche = numeroVente.Trim();

        var vente = await ConstruireRequeteDetail()
            .FirstOrDefaultAsync(v => v.NumeroVente == recherche, jeton)
            .ConfigureAwait(false);

        return vente is null ? null : Projeter(vente);
    }

    public async Task<IReadOnlyList<ResumeVenteDto>> RechercherAsync(
        DateTime? debut = null,
        DateTime? fin = null,
        int? clientId = null,
        int? utilisateurId = null,
        StatutVente? statut = null,
        string? recherche = null,
        int limite = 500,
        CancellationToken jeton = default)
    {
        var requete = _uniteDeTravail.Depot<Vente>().Requete();

        if (debut.HasValue)
        {
            requete = requete.Where(v => v.DateVente >= debut.Value);
        }

        if (fin.HasValue)
        {
            requete = requete.Where(v => v.DateVente <= fin.Value);
        }

        if (clientId.HasValue)
        {
            requete = requete.Where(v => v.ClientId == clientId.Value);
        }

        if (utilisateurId.HasValue)
        {
            requete = requete.Where(v => v.UtilisateurId == utilisateurId.Value);
        }

        if (statut.HasValue)
        {
            requete = requete.Where(v => v.Statut == statut.Value);
        }

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var terme = recherche.Trim().ToLowerInvariant();

            requete = requete.Where(v =>
                v.NumeroVente.ToLower().Contains(terme)
                || (v.Client != null && (v.Client.Nom + " " + v.Client.Prenom).ToLower().Contains(terme))
                || (v.Client != null && v.Client.Telephone != null && v.Client.Telephone.Contains(terme)));
        }

        var resultats = await requete
            .OrderByDescending(v => v.DateVente)
            .ThenByDescending(v => v.Id)
            .Take(limite)
            .Select(v => new
            {
                v.Id,
                v.NumeroVente,
                v.DateVente,
                ClientNom = v.Client != null ? v.Client.Prenom + " " + v.Client.Nom : null,
                UtilisateurNom = v.Utilisateur.Prenom + " " + v.Utilisateur.Nom,
                NombreArticles = v.Lignes.Sum(l => (int?)l.Quantite) ?? 0,
                v.Total,
                v.MontantPaye,
                v.Statut,
                Modes = v.Paiements.Select(p => p.ModePaiement).Distinct().ToList()
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return resultats.Select(v => new ResumeVenteDto
        {
            Id = v.Id,
            NumeroVente = v.NumeroVente,
            DateVente = v.DateVente,
            ClientNom = string.IsNullOrWhiteSpace(v.ClientNom) ? null : v.ClientNom.Trim(),
            UtilisateurNom = v.UtilisateurNom.Trim(),
            NombreArticles = v.NombreArticles,
            Total = v.Total,
            MontantPaye = v.MontantPaye,
            Statut = v.Statut,
            LibelleStatut = v.Statut.Libelle(),
            ModesPaiement = string.Join(", ", v.Modes.Select(m => m.Libelle()))
        }).ToList();
    }

    // ------------------------------------------------------------------
    // Utilitaires internes
    // ------------------------------------------------------------------

    private IQueryable<Vente> ConstruireRequeteDetail() =>
        _uniteDeTravail.Depot<Vente>().Requete()
            .Include(v => v.Client)
            .Include(v => v.Utilisateur)
            .Include(v => v.Paiements)
            .Include(v => v.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(vp => vp.Produit)
            .Include(v => v.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(vp => vp.Couleur)
            .Include(v => v.Lignes).ThenInclude(l => l.VarianteProduit).ThenInclude(vp => vp.Taille);

    private async Task<VenteDto> ObtenirObligatoireAsync(int venteId, CancellationToken jeton)
    {
        // Le suivi est vidé pour relire la vente telle qu'elle a été écrite,
        // sans reprendre l'état en mémoire de la transaction précédente.
        _uniteDeTravail.ViderSuivi();

        return await ObtenirAsync(venteId, jeton).ConfigureAwait(false)
               ?? throw new EntiteIntrouvableException("Vente", venteId);
    }

    private static VenteDto Projeter(Vente vente) => new()
    {
        Id = vente.Id,
        NumeroVente = vente.NumeroVente,
        ClientId = vente.ClientId,
        ClientNom = vente.Client is null ? null : vente.Client.NomComplet,
        ClientTelephone = vente.Client?.Telephone,
        UtilisateurId = vente.UtilisateurId,
        UtilisateurNom = vente.Utilisateur?.NomComplet ?? string.Empty,
        DateVente = vente.DateVente,
        SousTotal = vente.SousTotal,
        Remise = vente.Remise,
        Total = vente.Total,
        MontantPaye = vente.MontantPaye,
        Statut = vente.Statut,
        LibelleStatut = vente.Statut.Libelle(),
        Notes = vente.Notes,
        Lignes = vente.Lignes.Select(l => new LigneVenteDto
        {
            Id = l.Id,
            VarianteProduitId = l.VarianteProduitId,
            Designation = l.VarianteProduit.Designation,
            Sku = l.VarianteProduit.SKU,
            CodeBarres = l.VarianteProduit.CodeBarres,
            Couleur = l.VarianteProduit.Couleur?.Nom ?? string.Empty,
            Taille = l.VarianteProduit.Taille?.Nom ?? string.Empty,
            Quantite = l.Quantite,
            PrixUnitaire = l.PrixUnitaire,
            Remise = l.Remise,
            Total = l.Total,
            QuantiteRetournee = l.QuantiteRetournee
        }).ToList(),
        Paiements = vente.Paiements.Select(p => new PaiementDto
        {
            Id = p.Id,
            ModePaiement = p.ModePaiement,
            LibelleModePaiement = p.ModePaiement.Libelle(),
            Montant = p.Montant,
            DatePaiement = p.DatePaiement,
            Reference = p.Reference
        }).ToList()
    };

    /// <summary>Contrôle la cohérence du panier avant toute écriture.</summary>
    private static List<LigneVenteDemande> PreparerLignes(DemandeVente demande)
    {
        if (demande.Lignes.Count == 0)
        {
            throw new ValidationMetierException("Le panier est vide. Ajoutez au moins un article.");
        }

        var erreurs = new List<string>();

        foreach (var ligne in demande.Lignes)
        {
            if (ligne.Quantite <= 0)
            {
                erreurs.Add("La quantité de chaque article doit être supérieure à zéro.");
            }

            if (ligne.Remise < 0)
            {
                erreurs.Add("Une remise ne peut pas être négative.");
            }
        }

        if (erreurs.Count > 0)
        {
            throw new ValidationMetierException(erreurs.Distinct().ToList());
        }

        return demande.Lignes.ToList();
    }

    /// <summary>
    /// Charge les déclinaisons du panier et vérifie qu'elles sont toutes
    /// vendables. Une seule requête suffit, quel que soit le nombre d'articles.
    /// </summary>
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

        var manquants = ids.Where(id => !variantes.ContainsKey(id)).ToList();

        if (manquants.Count > 0)
        {
            throw new EntiteIntrouvableException(
                "Un article du panier n'existe plus dans le catalogue. Veuillez le retirer du panier.");
        }

        var inactifs = variantes.Values
            .Where(v => !v.Actif || !v.Produit.Actif)
            .Select(v => v.Designation)
            .ToList();

        if (inactifs.Count > 0)
        {
            throw new ValidationMetierException(
                inactifs.Select(d => $"L'article « {d} » n'est plus proposé à la vente.").ToList());
        }

        return variantes;
    }

    private async Task ControlerClientAsync(int? clientId, CancellationToken jeton)
    {
        if (!clientId.HasValue)
        {
            return;
        }

        var client = await _uniteDeTravail.Depot<Client>().Requete()
            .FirstOrDefaultAsync(c => c.Id == clientId.Value, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Client", clientId.Value);

        if (!client.Actif)
        {
            throw new ValidationMetierException(
                $"Le client « {client.NomComplet} » est désactivé. Réactivez sa fiche ou vendez sans client.");
        }
    }

    /// <summary>
    /// Vérifie les règlements présentés. Le total encaissé ne peut pas
    /// dépasser le montant dû : la monnaie rendue est calculée en caisse et
    /// n'entre pas dans la comptabilité de la vente.
    /// </summary>
    private static decimal ControlerPaiements(IReadOnlyList<PaiementDemande> paiements, decimal totalDu)
    {
        if (paiements.Any(p => p.Montant < 0))
        {
            throw new ValidationMetierException("Le montant d'un règlement ne peut pas être négatif.");
        }

        var total = paiements.Sum(p => p.Montant);

        if (total > totalDu)
        {
            throw new ValidationMetierException(
                $"Le total des règlements ({total:N2} DA) dépasse le montant dû ({totalDu:N2} DA). " +
                "Saisissez le montant dû et rendez la monnaie au client.");
        }

        return total;
    }
}
