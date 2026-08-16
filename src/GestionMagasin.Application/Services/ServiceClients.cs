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

/// <inheritdoc cref="IServiceClients"/>
public class ServiceClients : IServiceClients
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IServiceAudit _audit;
    private readonly ISessionUtilisateur _session;
    private readonly IFournisseurHorodatage _horodatage;

    public ServiceClients(
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

    public async Task<ClientDto> CreerAsync(DemandeClient demande, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererClients);

        await ValiderAsync(demande, null, jeton).ConfigureAwait(false);

        var client = new Client
        {
            Nom = demande.Nom.Trim(),
            Prenom = demande.Prenom?.Trim(),
            Telephone = demande.Telephone?.Trim(),
            Email = demande.Email?.Trim(),
            Adresse = demande.Adresse?.Trim(),
            Notes = demande.Notes?.Trim(),
            Actif = true,
            DateCreation = _horodatage.MaintenantUtc
        };

        await _uniteDeTravail.Depot<Client>().AjouterAsync(client, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        await _audit.TracerEtEnregistrerAsync(
            ActionsAudit.Creation,
            nameof(Client),
            client.Id,
            $"Client « {client.NomComplet} » créé.",
            jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(client.Id, jeton).ConfigureAwait(false);
    }

    public async Task<ClientDto> ModifierAsync(
        int clientId,
        DemandeClient demande,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererClients);

        await ValiderAsync(demande, clientId, jeton).ConfigureAwait(false);

        var client = await _uniteDeTravail.Depot<Client>().RequeteSuivie()
            .FirstOrDefaultAsync(c => c.Id == clientId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Client", clientId);

        client.Nom = demande.Nom.Trim();
        client.Prenom = demande.Prenom?.Trim();
        client.Telephone = demande.Telephone?.Trim();
        client.Email = demande.Email?.Trim();
        client.Adresse = demande.Adresse?.Trim();
        client.Notes = demande.Notes?.Trim();

        await _audit.TracerAsync(
            ActionsAudit.Modification,
            nameof(Client),
            client.Id,
            $"Fiche du client « {client.NomComplet} » modifiée.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(clientId, jeton).ConfigureAwait(false);
    }

    public async Task<ClientDto> DefinirEtatAsync(int clientId, bool actif, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererClients);

        var client = await _uniteDeTravail.Depot<Client>().RequeteSuivie()
            .FirstOrDefaultAsync(c => c.Id == clientId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Client", clientId);

        client.Actif = actif;

        await _audit.TracerAsync(
            actif ? ActionsAudit.Activation : ActionsAudit.Desactivation,
            nameof(Client),
            client.Id,
            actif
                ? $"Client « {client.NomComplet} » réactivé."
                : $"Client « {client.NomComplet} » désactivé. Son historique d'achats est conservé.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(clientId, jeton).ConfigureAwait(false);
    }

    public async Task<ClientDto?> ObtenirAsync(int clientId, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirClients);

        return await ConstruireProjection()
            .FirstOrDefaultAsync(c => c.Id == clientId, jeton)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClientDto>> RechercherAsync(
        string? recherche = null,
        bool inclureInactifs = false,
        int limite = 500,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirClients);

        var requete = _uniteDeTravail.Depot<Client>().Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(c => c.Actif);
        }

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var terme = recherche.Trim().ToLowerInvariant();

            requete = requete.Where(c =>
                c.Nom.ToLower().Contains(terme)
                || (c.Prenom != null && c.Prenom.ToLower().Contains(terme))
                || (c.Telephone != null && c.Telephone.Contains(terme))
                || (c.Email != null && c.Email.ToLower().Contains(terme)));
        }

        return await ConstruireProjection(requete)
            .OrderBy(c => c.Nom)
            .ThenBy(c => c.Prenom)
            .Take(limite)
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ResumeVenteDto>> ObtenirHistoriqueAsync(
        int clientId,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirClients);

        var ventes = await _uniteDeTravail.Depot<Vente>().Requete()
            .Where(v => v.ClientId == clientId)
            .OrderByDescending(v => v.DateVente)
            .Select(v => new
            {
                v.Id,
                v.NumeroVente,
                v.DateVente,
                UtilisateurNom = v.Utilisateur.Prenom + " " + v.Utilisateur.Nom,
                NombreArticles = v.Lignes.Sum(l => (int?)l.Quantite) ?? 0,
                v.Total,
                v.MontantPaye,
                v.Statut
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return ventes.Select(v => new ResumeVenteDto
        {
            Id = v.Id,
            NumeroVente = v.NumeroVente,
            DateVente = v.DateVente,
            UtilisateurNom = v.UtilisateurNom.Trim(),
            NombreArticles = v.NombreArticles,
            Total = v.Total,
            MontantPaye = v.MontantPaye,
            Statut = v.Statut,
            LibelleStatut = v.Statut.Libelle()
        }).ToList();
    }

    // ------------------------------------------------------------------

    private IQueryable<ClientDto> ConstruireProjection(IQueryable<Client>? source = null) =>
        (source ?? _uniteDeTravail.Depot<Client>().Requete())
        .Select(c => new ClientDto
        {
            Id = c.Id,
            Nom = c.Nom,
            Prenom = c.Prenom,
            Telephone = c.Telephone,
            Email = c.Email,
            Adresse = c.Adresse,
            Notes = c.Notes,
            Actif = c.Actif,
            DateCreation = c.DateCreation,
            // Les ventes annulées sont écartées du cumul : elles ne
            // correspondent à aucun achat réel.
            NombreAchats = c.Ventes.Count(v => v.Statut != StatutVente.Annulee),
            TotalAchats = c.Ventes
                .Where(v => v.Statut != StatutVente.Annulee)
                .Sum(v => (decimal?)v.Total) ?? 0m,
            DernierAchat = c.Ventes
                .Where(v => v.Statut != StatutVente.Annulee)
                .Max(v => (DateTime?)v.DateVente)
        });

    private async Task<ClientDto> ObtenirObligatoireAsync(int clientId, CancellationToken jeton)
    {
        _uniteDeTravail.ViderSuivi();

        return await ObtenirAsync(clientId, jeton).ConfigureAwait(false)
               ?? throw new EntiteIntrouvableException("Client", clientId);
    }

    private async Task ValiderAsync(DemandeClient demande, int? idExistant, CancellationToken jeton)
    {
        var erreurs = new List<string>();

        if (string.IsNullOrWhiteSpace(demande.Nom))
        {
            erreurs.Add("Le nom du client est obligatoire.");
        }

        if (!string.IsNullOrWhiteSpace(demande.Email) && !demande.Email.Contains('@'))
        {
            erreurs.Add("L'adresse e-mail saisie n'est pas valide.");
        }

        // Le téléphone identifie le client au comptoir : un doublon est
        // signalé pour éviter deux fiches pour la même personne.
        if (!string.IsNullOrWhiteSpace(demande.Telephone))
        {
            var telephone = demande.Telephone.Trim();

            var doublon = await _uniteDeTravail.Depot<Client>().Requete()
                .AnyAsync(c => c.Telephone == telephone && (idExistant == null || c.Id != idExistant), jeton)
                .ConfigureAwait(false);

            if (doublon)
            {
                erreurs.Add($"Un client utilise déjà le numéro de téléphone {telephone}.");
            }
        }

        if (erreurs.Count > 0)
        {
            throw new ValidationMetierException(erreurs);
        }
    }
}
