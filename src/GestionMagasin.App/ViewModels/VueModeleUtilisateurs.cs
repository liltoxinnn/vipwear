using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Application.Validators;
using GestionMagasin.Domain.Securite;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>Permission présentée avec sa case à cocher dans l'écran des rôles.</summary>
public partial class PermissionSelectionnable : ObservableObject
{
    public required string Code { get; init; }

    public required string Nom { get; init; }

    public string? Description { get; init; }

    public required string Categorie { get; init; }

    [ObservableProperty]
    private bool _accordee;
}

/// <summary>
/// Gestion des comptes, des rôles et de leurs permissions, ainsi que
/// consultation du journal d'audit.
/// </summary>
public partial class VueModeleUtilisateurs : VueModeleBase
{
    private readonly IServiceUtilisateurs _utilisateurs;
    private readonly ISessionUtilisateur _session;

    public VueModeleUtilisateurs(
        IServiceUtilisateurs utilisateurs,
        ISessionUtilisateur session,
        IServiceDialogue dialogue,
        ILogger<VueModeleUtilisateurs> journal)
        : base(dialogue, journal)
    {
        _utilisateurs = utilisateurs;
        _session = session;
    }

    public override string Titre => "Utilisateurs";

    public override string SousTitre => "Comptes, rôles, permissions et journal d'audit";

    public ObservableCollection<UtilisateurDto> Utilisateurs { get; } = [];

    public ObservableCollection<RoleDto> Roles { get; } = [];

    public ObservableCollection<PermissionSelectionnable> Permissions { get; } = [];

    public ObservableCollection<JournalAuditDto> Journal { get; } = [];

    [ObservableProperty]
    private UtilisateurDto? _utilisateurSelectionne;

    [ObservableProperty]
    private RoleDto? _roleSelectionne;

    // --- Formulaire de compte ---

    [ObservableProperty]
    private bool _modeEdition;

    [ObservableProperty]
    private int? _identifiantEdite;

    [ObservableProperty]
    private string _nom = string.Empty;

    [ObservableProperty]
    private string _prenom = string.Empty;

    [ObservableProperty]
    private string _nomUtilisateur = string.Empty;

    [ObservableProperty]
    private string _motDePasse = string.Empty;

    [ObservableProperty]
    private RoleDto? _roleAttribue;

    /// <summary>Indication affichée sous le champ mot de passe.</summary>
    public string AideMotDePasse => IdentifiantEdite.HasValue
        ? "Laissez vide pour conserver le mot de passe actuel."
        : $"Au moins {ValidateurMotDePasse.LongueurMinimale} caractères, dont une lettre et un chiffre.";

    public bool PeutVoirJournal => _session.Possede(CodesPermissions.VoirJournalAudit);

    public override async Task ChargerAsync() =>
        await ExecuterAsync(async () =>
        {
            await ChargerComptesAsync().ConfigureAwait(true);
            await ChargerRolesAsync().ConfigureAwait(true);
            await ChargerJournalAsync().ConfigureAwait(true);
        }, contexteJournal: "chargement des utilisateurs").ConfigureAwait(true);

    private async Task ChargerComptesAsync()
    {
        var comptes = await _utilisateurs.ListerAsync().ConfigureAwait(true);

        var idPrecedent = UtilisateurSelectionne?.Id;

        Utilisateurs.Clear();
        foreach (var compte in comptes)
        {
            Utilisateurs.Add(compte);
        }

        UtilisateurSelectionne = idPrecedent is null
            ? Utilisateurs.FirstOrDefault()
            : Utilisateurs.FirstOrDefault(u => u.Id == idPrecedent) ?? Utilisateurs.FirstOrDefault();
    }

    private async Task ChargerRolesAsync()
    {
        var roles = await _utilisateurs.ListerRolesAsync().ConfigureAwait(true);

        var idPrecedent = RoleSelectionne?.Id;

        Roles.Clear();
        foreach (var role in roles)
        {
            Roles.Add(role);
        }

        RoleSelectionne = idPrecedent is null
            ? Roles.FirstOrDefault()
            : Roles.FirstOrDefault(r => r.Id == idPrecedent) ?? Roles.FirstOrDefault();
    }

    private async Task ChargerJournalAsync()
    {
        Journal.Clear();

        if (!PeutVoirJournal)
        {
            return;
        }

        var entrees = await _utilisateurs.ConsulterJournalAsync(limite: 300).ConfigureAwait(true);

        foreach (var entree in entrees)
        {
            Journal.Add(entree);
        }
    }

    partial void OnRoleSelectionneChanged(RoleDto? value)
    {
        _ = ChargerPermissionsAsync(value?.Id);
    }

    private async Task ChargerPermissionsAsync(int? roleId)
    {
        Permissions.Clear();

        if (roleId is null)
        {
            return;
        }

        var permissions = await _utilisateurs.ListerPermissionsAsync(roleId).ConfigureAwait(true);

        foreach (var permission in permissions)
        {
            Permissions.Add(new PermissionSelectionnable
            {
                Code = permission.Code,
                Nom = permission.Nom,
                Description = permission.Description,
                Categorie = permission.Categorie,
                Accordee = permission.Accordee
            });
        }
    }

    // ==================================================================
    // Comptes
    // ==================================================================

    [RelayCommand]
    private void NouveauCompte()
    {
        IdentifiantEdite = null;
        Nom = string.Empty;
        Prenom = string.Empty;
        NomUtilisateur = string.Empty;
        MotDePasse = string.Empty;
        RoleAttribue = Roles.FirstOrDefault(r => r.Nom == NomsRoles.Caissier) ?? Roles.FirstOrDefault();

        OnPropertyChanged(nameof(AideMotDePasse));
        ModeEdition = true;
    }

    [RelayCommand]
    private void ModifierCompte()
    {
        if (UtilisateurSelectionne is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un compte.", "Aucune sélection");
            return;
        }

        IdentifiantEdite = UtilisateurSelectionne.Id;
        Nom = UtilisateurSelectionne.Nom;
        Prenom = UtilisateurSelectionne.Prenom;
        NomUtilisateur = UtilisateurSelectionne.NomUtilisateur;
        MotDePasse = string.Empty;
        RoleAttribue = Roles.FirstOrDefault(r => r.Id == UtilisateurSelectionne.RoleId);

        OnPropertyChanged(nameof(AideMotDePasse));
        ModeEdition = true;
    }

    [RelayCommand]
    private void AnnulerEdition() => ModeEdition = false;

    [RelayCommand]
    private async Task EnregistrerCompteAsync()
    {
        if (RoleAttribue is null)
        {
            Dialogue.Avertir("Sélectionnez le rôle à attribuer à ce compte.", "Rôle manquant");
            return;
        }

        var demande = new DemandeUtilisateur
        {
            Nom = Nom,
            Prenom = Prenom,
            NomUtilisateur = NomUtilisateur,
            MotDePasse = string.IsNullOrWhiteSpace(MotDePasse) ? null : MotDePasse,
            RoleId = RoleAttribue.Id
        };

        var reussi = await ExecuterAsync(
            async () =>
            {
                var enregistre = IdentifiantEdite.HasValue
                    ? await _utilisateurs.ModifierAsync(IdentifiantEdite.Value, demande).ConfigureAwait(true)
                    : await _utilisateurs.CreerAsync(demande).ConfigureAwait(true);

                await ChargerComptesAsync().ConfigureAwait(true);
                await ChargerRolesAsync().ConfigureAwait(true);

                UtilisateurSelectionne = Utilisateurs.FirstOrDefault(u => u.Id == enregistre.Id)
                                         ?? UtilisateurSelectionne;
            },
            "Compte enregistré avec succès.",
            "enregistrement d'un compte").ConfigureAwait(true);

        if (reussi)
        {
            MotDePasse = string.Empty;
            ModeEdition = false;
        }
    }

    [RelayCommand]
    private async Task BasculerEtatCompteAsync()
    {
        if (UtilisateurSelectionne is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un compte.", "Aucune sélection");
            return;
        }

        var activer = !UtilisateurSelectionne.Actif;
        var identifiant = UtilisateurSelectionne.Id;
        var nom = UtilisateurSelectionne.NomUtilisateur;

        if (!Dialogue.Confirmer(
                activer
                    ? $"Réactiver le compte « {nom} » ?"
                    : $"Désactiver le compte « {nom} » ? Cet utilisateur ne pourra plus se connecter.",
                "Confirmation"))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _utilisateurs.DefinirEtatAsync(identifiant, activer).ConfigureAwait(true);
                await ChargerComptesAsync().ConfigureAwait(true);
            },
            activer ? "Compte réactivé." : "Compte désactivé.",
            "changement d'état d'un compte").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ReinitialiserMotDePasseAsync()
    {
        if (UtilisateurSelectionne is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un compte.", "Aucune sélection");
            return;
        }

        var identifiant = UtilisateurSelectionne.Id;
        var nom = UtilisateurSelectionne.NomUtilisateur;

        var nouveau = Dialogue.DemanderTexte(
            $"Nouveau mot de passe pour le compte « {nom} » :" + Environment.NewLine +
            $"Au moins {ValidateurMotDePasse.LongueurMinimale} caractères, dont une lettre et un chiffre.",
            "Réinitialiser le mot de passe");

        if (string.IsNullOrWhiteSpace(nouveau))
        {
            return;
        }

        await ExecuterAsync(
            () => _utilisateurs.ReinitialiserMotDePasseAsync(identifiant, nouveau),
            "Mot de passe réinitialisé. Communiquez-le à l'utilisateur concerné.",
            "réinitialisation d'un mot de passe").ConfigureAwait(true);
    }

    // ==================================================================
    // Rôles et permissions
    // ==================================================================

    [RelayCommand]
    private async Task NouveauRoleAsync()
    {
        var nom = Dialogue.DemanderTexte(
            "Nom du nouveau rôle :",
            "Créer un rôle");

        if (string.IsNullOrWhiteSpace(nom))
        {
            return;
        }

        var description = Dialogue.DemanderTexte(
            "Description du rôle (facultative) :",
            "Créer un rôle") ?? string.Empty;

        await ExecuterAsync(
            async () =>
            {
                var role = await _utilisateurs
                    .EnregistrerRoleAsync(null, nom, description)
                    .ConfigureAwait(true);

                await ChargerRolesAsync().ConfigureAwait(true);

                RoleSelectionne = Roles.FirstOrDefault(r => r.Id == role.Id) ?? RoleSelectionne;
            },
            "Rôle créé avec succès. Attribuez-lui maintenant ses permissions.",
            "création d'un rôle").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task EnregistrerPermissionsAsync()
    {
        if (RoleSelectionne is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un rôle.", "Aucune sélection");
            return;
        }

        var roleId = RoleSelectionne.Id;

        var codes = Permissions
            .Where(p => p.Accordee)
            .Select(p => p.Code)
            .ToList();

        await ExecuterAsync(
            async () =>
            {
                await _utilisateurs.DefinirPermissionsAsync(roleId, codes).ConfigureAwait(true);
                await ChargerRolesAsync().ConfigureAwait(true);
                await ChargerPermissionsAsync(roleId).ConfigureAwait(true);
            },
            "Permissions enregistrées. Elles s'appliqueront à la prochaine connexion des utilisateurs concernés.",
            "enregistrement des permissions d'un rôle").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SupprimerRoleAsync()
    {
        if (RoleSelectionne is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un rôle.", "Aucune sélection");
            return;
        }

        var roleId = RoleSelectionne.Id;
        var nom = RoleSelectionne.Nom;

        if (!Dialogue.Confirmer(
                $"Êtes-vous sûr de vouloir supprimer le rôle « {nom} » ?",
                "Supprimer le rôle"))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _utilisateurs.SupprimerRoleAsync(roleId).ConfigureAwait(true);
                await ChargerRolesAsync().ConfigureAwait(true);
            },
            "Rôle supprimé avec succès.",
            "suppression d'un rôle").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RafraichirJournalAsync() =>
        await ExecuterAsync(ChargerJournalAsync, contexteJournal: "chargement du journal d'audit")
            .ConfigureAwait(true);
}
