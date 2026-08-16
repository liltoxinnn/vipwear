using GestionMagasin.Application.Common;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Application.Validators;
using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Domain.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.Application.Services;

/// <inheritdoc cref="IServiceAuthentification"/>
public class ServiceAuthentification : IServiceAuthentification
{
    /// <summary>
    /// Message unique renvoyé pour tout échec de connexion. Il ne distingue
    /// pas un compte inexistant d'un mot de passe erroné.
    /// </summary>
    private const string MessageEchec = "Identifiant ou mot de passe incorrect.";

    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IHacheurMotDePasse _hacheur;
    private readonly SessionUtilisateur _session;
    private readonly IServiceAudit _audit;
    private readonly IFournisseurHorodatage _horodatage;
    private readonly ILogger<ServiceAuthentification> _journal;

    public ServiceAuthentification(
        IUniteDeTravail uniteDeTravail,
        IHacheurMotDePasse hacheur,
        SessionUtilisateur session,
        IServiceAudit audit,
        IFournisseurHorodatage horodatage,
        ILogger<ServiceAuthentification> journal)
    {
        _uniteDeTravail = uniteDeTravail;
        _hacheur = hacheur;
        _session = session;
        _audit = audit;
        _horodatage = horodatage;
        _journal = journal;
    }

    public async Task<ResultatConnexion> ConnecterAsync(
        string nomUtilisateur,
        string motDePasse,
        CancellationToken jeton = default)
    {
        if (string.IsNullOrWhiteSpace(nomUtilisateur) || string.IsNullOrWhiteSpace(motDePasse))
        {
            return ResultatConnexion.Echec("Veuillez saisir votre identifiant et votre mot de passe.");
        }

        var utilisateur = await _uniteDeTravail.Utilisateurs
            .ObtenirParNomUtilisateurAsync(nomUtilisateur, jeton)
            .ConfigureAwait(false);

        if (utilisateur is null)
        {
            // Le mot de passe est tout de même vérifié contre une empreinte
            // factice : la durée de la réponse ne permet pas de deviner si le
            // compte existe.
            _hacheur.Verifier(motDePasse, EmpreinteFactice);

            _journal.LogWarning("Tentative de connexion avec un identifiant inconnu : {Identifiant}.", nomUtilisateur);
            return ResultatConnexion.Echec(MessageEchec);
        }

        if (!_hacheur.Verifier(motDePasse, utilisateur.PasswordHash))
        {
            _journal.LogWarning("Mot de passe incorrect pour le compte {Identifiant}.", utilisateur.NomUtilisateur);

            await TracerEchecAsync(utilisateur.Id, utilisateur.NomUtilisateur, jeton).ConfigureAwait(false);

            return ResultatConnexion.Echec(MessageEchec);
        }

        if (!utilisateur.Actif)
        {
            _journal.LogWarning("Connexion refusée : le compte {Identifiant} est désactivé.", utilisateur.NomUtilisateur);

            return ResultatConnexion.Echec(
                "Ce compte est désactivé. Veuillez contacter votre administrateur.");
        }

        var permissions = await _uniteDeTravail.Utilisateurs
            .ObtenirPermissionsDuRoleAsync(utilisateur.RoleId, jeton)
            .ConfigureAwait(false);

        _session.Ouvrir(utilisateur, permissions);

        await MettreAJourConnexionAsync(utilisateur.Id, motDePasse, jeton).ConfigureAwait(false);

        await _audit.TracerEtEnregistrerAsync(
            ActionsAudit.Connexion,
            nameof(Utilisateur),
            utilisateur.Id,
            $"Connexion de {utilisateur.NomComplet} ({utilisateur.NomUtilisateur}).",
            jeton).ConfigureAwait(false);

        _journal.LogInformation("Connexion réussie pour {Identifiant}.", utilisateur.NomUtilisateur);

        // Le mot de passe livré à l'installation doit être remplacé.
        var motDePasseAChanger = motDePasse == ComptesParDefaut.MotDePasseAdministrateur;

        return ResultatConnexion.Succes(motDePasseAChanger);
    }

    public async Task DeconnecterAsync(CancellationToken jeton = default)
    {
        if (_session.EstAuthentifie)
        {
            await _audit.TracerEtEnregistrerAsync(
                ActionsAudit.Deconnexion,
                nameof(Utilisateur),
                _session.UtilisateurId,
                $"Déconnexion de {_session.NomComplet}.",
                jeton).ConfigureAwait(false);
        }

        _session.Fermer();
    }

    public async Task ChangerMotDePasseAsync(
        string ancienMotDePasse,
        string nouveauMotDePasse,
        CancellationToken jeton = default)
    {
        var utilisateurId = _session.UtilisateurIdObligatoire;

        var utilisateur = await _uniteDeTravail.Utilisateurs.RequeteSuivie()
            .FirstOrDefaultAsync(u => u.Id == utilisateurId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Utilisateur", utilisateurId);

        if (!_hacheur.Verifier(ancienMotDePasse, utilisateur.PasswordHash))
        {
            throw new ValidationMetierException("Le mot de passe actuel est incorrect.");
        }

        ValidateurMotDePasse.Valider(nouveauMotDePasse);

        if (_hacheur.Verifier(nouveauMotDePasse, utilisateur.PasswordHash))
        {
            throw new ValidationMetierException(
                "Le nouveau mot de passe doit être différent de l'ancien.");
        }

        utilisateur.PasswordHash = _hacheur.Hacher(nouveauMotDePasse);

        await _audit.TracerAsync(
            ActionsAudit.ChangementMotDePasse,
            nameof(Utilisateur),
            utilisateur.Id,
            $"Changement de mot de passe du compte {utilisateur.NomUtilisateur}.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation("Mot de passe modifié pour {Identifiant}.", utilisateur.NomUtilisateur);
    }

    /// <summary>
    /// Met à jour la date de dernière connexion et renforce l'empreinte du
    /// mot de passe si elle a été produite avec des paramètres devenus faibles.
    /// </summary>
    private async Task MettreAJourConnexionAsync(int utilisateurId, string motDePasse, CancellationToken jeton)
    {
        var suivi = await _uniteDeTravail.Utilisateurs.RequeteSuivie()
            .FirstAsync(u => u.Id == utilisateurId, jeton)
            .ConfigureAwait(false);

        suivi.DerniereConnexion = _horodatage.MaintenantUtc;

        if (_hacheur.NecessiteMiseAJour(suivi.PasswordHash))
        {
            suivi.PasswordHash = _hacheur.Hacher(motDePasse);
            _journal.LogInformation("Empreinte du mot de passe renforcée pour {Identifiant}.", suivi.NomUtilisateur);
        }

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
    }

    private async Task TracerEchecAsync(int utilisateurId, string nomUtilisateur, CancellationToken jeton)
    {
        var entree = new JournalAudit
        {
            UtilisateurId = utilisateurId,
            Action = ActionsAudit.EchecConnexion,
            TypeEntite = nameof(Utilisateur),
            EntiteId = utilisateurId,
            Description = $"Échec de connexion pour le compte {nomUtilisateur}.",
            DateCreation = _horodatage.MaintenantUtc
        };

        await _uniteDeTravail.Depot<JournalAudit>().AjouterAsync(entree, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
    }

    /// <summary>
    /// Empreinte sans utilité fonctionnelle, calculée sur un mot de passe
    /// aléatoire. Elle sert uniquement à égaliser le temps de réponse.
    /// </summary>
    private static readonly string EmpreinteFactice =
        "PBKDF2-SHA512$210000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
}
