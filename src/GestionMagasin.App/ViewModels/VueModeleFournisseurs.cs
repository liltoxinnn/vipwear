using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Securite;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>Fiches fournisseurs et historique de leurs commandes.</summary>
public partial class VueModeleFournisseurs : VueModeleBase
{
    private readonly IServiceFournisseurs _fournisseurs;
    private readonly ISessionUtilisateur _session;

    public VueModeleFournisseurs(
        IServiceFournisseurs fournisseurs,
        ISessionUtilisateur session,
        IServiceDialogue dialogue,
        ILogger<VueModeleFournisseurs> journal)
        : base(dialogue, journal)
    {
        _fournisseurs = fournisseurs;
        _session = session;
    }

    public override string Titre => "Fournisseurs";

    public override string SousTitre => "Coordonnées, commandes et soldes dus";

    public ObservableCollection<FournisseurDto> Fournisseurs { get; } = [];

    public ObservableCollection<ResumeAchatDto> Historique { get; } = [];

    [ObservableProperty]
    private FournisseurDto? _fournisseurSelectionne;

    [ObservableProperty]
    private string _recherche = string.Empty;

    [ObservableProperty]
    private bool _inclureInactifs;

    // --- Formulaire ---

    [ObservableProperty]
    private bool _modeEdition;

    [ObservableProperty]
    private int? _identifiantEdite;

    [ObservableProperty]
    private string _nom = string.Empty;

    [ObservableProperty]
    private string _entreprise = string.Empty;

    [ObservableProperty]
    private string _telephone = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _adresse = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    public bool PeutGerer => _session.Possede(CodesPermissions.GererFournisseurs);

    public override async Task ChargerAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "chargement des fournisseurs")
            .ConfigureAwait(true);

    private async Task ChargerListeAsync()
    {
        var resultats = await _fournisseurs
            .RechercherAsync(Recherche, InclureInactifs)
            .ConfigureAwait(true);

        var idPrecedent = FournisseurSelectionne?.Id;

        Fournisseurs.Clear();
        foreach (var fournisseur in resultats)
        {
            Fournisseurs.Add(fournisseur);
        }

        FournisseurSelectionne = idPrecedent is null
            ? Fournisseurs.FirstOrDefault()
            : Fournisseurs.FirstOrDefault(f => f.Id == idPrecedent) ?? Fournisseurs.FirstOrDefault();
    }

    partial void OnFournisseurSelectionneChanged(FournisseurDto? value)
    {
        _ = ChargerHistoriqueAsync(value?.Id);
    }

    private async Task ChargerHistoriqueAsync(int? fournisseurId)
    {
        Historique.Clear();

        if (fournisseurId is null)
        {
            return;
        }

        var achats = await _fournisseurs.ObtenirHistoriqueAsync(fournisseurId.Value).ConfigureAwait(true);

        foreach (var achat in achats)
        {
            Historique.Add(achat);
        }
    }

    [RelayCommand]
    private async Task RechercherAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "recherche de fournisseurs")
            .ConfigureAwait(true);

    [RelayCommand]
    private void Nouveau()
    {
        if (!VerifierDroits())
        {
            return;
        }

        IdentifiantEdite = null;
        Nom = string.Empty;
        Entreprise = string.Empty;
        Telephone = string.Empty;
        Email = string.Empty;
        Adresse = string.Empty;
        Notes = string.Empty;

        ModeEdition = true;
    }

    [RelayCommand]
    private void Modifier()
    {
        if (FournisseurSelectionne is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un fournisseur.", "Aucune sélection");
            return;
        }

        if (!VerifierDroits())
        {
            return;
        }

        IdentifiantEdite = FournisseurSelectionne.Id;
        Nom = FournisseurSelectionne.Nom;
        Entreprise = FournisseurSelectionne.Entreprise ?? string.Empty;
        Telephone = FournisseurSelectionne.Telephone ?? string.Empty;
        Email = FournisseurSelectionne.Email ?? string.Empty;
        Adresse = FournisseurSelectionne.Adresse ?? string.Empty;
        Notes = FournisseurSelectionne.Notes ?? string.Empty;

        ModeEdition = true;
    }

    [RelayCommand]
    private void AnnulerEdition() => ModeEdition = false;

    [RelayCommand]
    private async Task EnregistrerAsync()
    {
        var demande = new DemandeFournisseur
        {
            Nom = Nom,
            Entreprise = Entreprise,
            Telephone = Telephone,
            Email = Email,
            Adresse = Adresse,
            Notes = Notes
        };

        var reussi = await ExecuterAsync(
            async () =>
            {
                var enregistre = IdentifiantEdite.HasValue
                    ? await _fournisseurs.ModifierAsync(IdentifiantEdite.Value, demande).ConfigureAwait(true)
                    : await _fournisseurs.CreerAsync(demande).ConfigureAwait(true);

                await ChargerListeAsync().ConfigureAwait(true);

                FournisseurSelectionne = Fournisseurs.FirstOrDefault(f => f.Id == enregistre.Id)
                                         ?? FournisseurSelectionne;
            },
            "Opération effectuée avec succès.",
            "enregistrement d'un fournisseur").ConfigureAwait(true);

        if (reussi)
        {
            ModeEdition = false;
        }
    }

    [RelayCommand]
    private async Task BasculerEtatAsync()
    {
        if (FournisseurSelectionne is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un fournisseur.", "Aucune sélection");
            return;
        }

        if (!VerifierDroits())
        {
            return;
        }

        var activer = !FournisseurSelectionne.Actif;
        var nom = FournisseurSelectionne.Nom;
        var identifiant = FournisseurSelectionne.Id;

        if (!Dialogue.Confirmer(
                activer
                    ? $"Réactiver le fournisseur « {nom} » ?"
                    : $"Désactiver le fournisseur « {nom} » ?" + Environment.NewLine +
                      "Aucune nouvelle commande ne pourra lui être passée, mais son historique sera conservé.",
                "Confirmation"))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _fournisseurs.DefinirEtatAsync(identifiant, activer).ConfigureAwait(true);
                await ChargerListeAsync().ConfigureAwait(true);
            },
            activer ? "Fournisseur réactivé." : "Fournisseur désactivé.",
            "changement d'état d'un fournisseur").ConfigureAwait(true);
    }

    private bool VerifierDroits()
    {
        if (PeutGerer)
        {
            return true;
        }

        Dialogue.Avertir(
            "Vous n'avez pas l'autorisation de gérer les fournisseurs.",
            "Action non autorisée");

        return false;
    }
}
