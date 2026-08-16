using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Domain.Securite;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Application.Services;

/// <inheritdoc cref="IServiceFournisseurs"/>
public class ServiceFournisseurs : IServiceFournisseurs
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IServiceAudit _audit;
    private readonly ISessionUtilisateur _session;
    private readonly IFournisseurHorodatage _horodatage;

    public ServiceFournisseurs(
        IUniteDeTravail uniteDeTravail,
        IServiceAudit audit,
        ISessionUtilisateur session,
        IFournisseurHorodatage horodatage)
    {
        _uniteDeTravail = uniteDeTravail;
        _audit = audit;
        _session = session;
        _horodatage = horodatage;
    }

    public async Task<FournisseurDto> CreerAsync(DemandeFournisseur demande, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererFournisseurs);

        Valider(demande);

        var fournisseur = new Fournisseur
        {
            Nom = demande.Nom.Trim(),
            Entreprise = demande.Entreprise?.Trim(),
            Telephone = demande.Telephone?.Trim(),
            Email = demande.Email?.Trim(),
            Adresse = demande.Adresse?.Trim(),
            Notes = demande.Notes?.Trim(),
            Actif = true,
            DateCreation = _horodatage.MaintenantUtc
        };

        await _uniteDeTravail.Depot<Fournisseur>().AjouterAsync(fournisseur, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        await _audit.TracerEtEnregistrerAsync(
            ActionsAudit.Creation,
            nameof(Fournisseur),
            fournisseur.Id,
            $"Fournisseur « {fournisseur.Nom} » créé.",
            jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(fournisseur.Id, jeton).ConfigureAwait(false);
    }

    public async Task<FournisseurDto> ModifierAsync(
        int fournisseurId,
        DemandeFournisseur demande,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererFournisseurs);

        Valider(demande);

        var fournisseur = await _uniteDeTravail.Depot<Fournisseur>().RequeteSuivie()
            .FirstOrDefaultAsync(f => f.Id == fournisseurId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Fournisseur", fournisseurId);

        fournisseur.Nom = demande.Nom.Trim();
        fournisseur.Entreprise = demande.Entreprise?.Trim();
        fournisseur.Telephone = demande.Telephone?.Trim();
        fournisseur.Email = demande.Email?.Trim();
        fournisseur.Adresse = demande.Adresse?.Trim();
        fournisseur.Notes = demande.Notes?.Trim();

        await _audit.TracerAsync(
            ActionsAudit.Modification,
            nameof(Fournisseur),
            fournisseur.Id,
            $"Fiche du fournisseur « {fournisseur.Nom} » modifiée.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(fournisseurId, jeton).ConfigureAwait(false);
    }

    public async Task<FournisseurDto> DefinirEtatAsync(
        int fournisseurId,
        bool actif,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererFournisseurs);

        var fournisseur = await _uniteDeTravail.Depot<Fournisseur>().RequeteSuivie()
            .FirstOrDefaultAsync(f => f.Id == fournisseurId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Fournisseur", fournisseurId);

        fournisseur.Actif = actif;

        await _audit.TracerAsync(
            actif ? ActionsAudit.Activation : ActionsAudit.Desactivation,
            nameof(Fournisseur),
            fournisseur.Id,
            actif
                ? $"Fournisseur « {fournisseur.Nom} » réactivé."
                : $"Fournisseur « {fournisseur.Nom} » désactivé. Son historique de commandes est conservé.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(fournisseurId, jeton).ConfigureAwait(false);
    }

    public async Task<FournisseurDto?> ObtenirAsync(int fournisseurId, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirAchats);

        return await ConstruireProjection()
            .FirstOrDefaultAsync(f => f.Id == fournisseurId, jeton)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FournisseurDto>> RechercherAsync(
        string? recherche = null,
        bool inclureInactifs = false,
        int limite = 500,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirAchats);

        var requete = _uniteDeTravail.Depot<Fournisseur>().Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(f => f.Actif);
        }

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var terme = recherche.Trim().ToLowerInvariant();

            requete = requete.Where(f =>
                f.Nom.ToLower().Contains(terme)
                || (f.Entreprise != null && f.Entreprise.ToLower().Contains(terme))
                || (f.Telephone != null && f.Telephone.Contains(terme)));
        }

        return await ConstruireProjection(requete)
            .OrderBy(f => f.Nom)
            .Take(limite)
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ResumeAchatDto>> ObtenirHistoriqueAsync(
        int fournisseurId,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirAchats);

        return await _uniteDeTravail.Depot<Achat>().Requete()
            .Where(a => a.FournisseurId == fournisseurId)
            .OrderByDescending(a => a.DateAchat)
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

    private IQueryable<FournisseurDto> ConstruireProjection(IQueryable<Fournisseur>? source = null) =>
        (source ?? _uniteDeTravail.Depot<Fournisseur>().Requete())
        .Select(f => new FournisseurDto
        {
            Id = f.Id,
            Nom = f.Nom,
            Entreprise = f.Entreprise,
            Telephone = f.Telephone,
            Email = f.Email,
            Adresse = f.Adresse,
            Notes = f.Notes,
            Actif = f.Actif,
            DateCreation = f.DateCreation,
            NombreCommandes = f.Achats.Count(a => a.Statut != StatutAchat.Annule),
            TotalCommandes = f.Achats
                .Where(a => a.Statut != StatutAchat.Annule)
                .Sum(a => (decimal?)a.Total) ?? 0m,
            SoldeDu = f.Achats
                .Where(a => a.Statut != StatutAchat.Annule)
                .Sum(a => (decimal?)a.MontantRestant) ?? 0m
        });

    private async Task<FournisseurDto> ObtenirObligatoireAsync(int fournisseurId, CancellationToken jeton)
    {
        _uniteDeTravail.ViderSuivi();

        return await ObtenirAsync(fournisseurId, jeton).ConfigureAwait(false)
               ?? throw new EntiteIntrouvableException("Fournisseur", fournisseurId);
    }

    private static void Valider(DemandeFournisseur demande)
    {
        var erreurs = new List<string>();

        if (string.IsNullOrWhiteSpace(demande.Nom))
        {
            erreurs.Add("Le nom du fournisseur est obligatoire.");
        }

        if (!string.IsNullOrWhiteSpace(demande.Email) && !demande.Email.Contains('@'))
        {
            erreurs.Add("L'adresse e-mail saisie n'est pas valide.");
        }

        if (erreurs.Count > 0)
        {
            throw new ValidationMetierException(erreurs);
        }
    }
}
