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

/// <summary>Fiches clients et historique de leurs achats.</summary>
public partial class VueModeleClients : VueModeleBase
{
    private readonly IServiceClients _clients;
    private readonly ISessionUtilisateur _session;

    public VueModeleClients(
        IServiceClients clients,
        ISessionUtilisateur session,
        IServiceDialogue dialogue,
        ILogger<VueModeleClients> journal)
        : base(dialogue, journal)
    {
        _clients = clients;
        _session = session;
    }

    public override string Titre => "Clients";

    public override string SousTitre => "Fiches clients et historique des achats";

    public ObservableCollection<ClientDto> Clients { get; } = [];

    public ObservableCollection<ResumeVenteDto> Historique { get; } = [];

    [ObservableProperty]
    private ClientDto? _clientSelectionne;

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
    private string _prenom = string.Empty;

    [ObservableProperty]
    private string _telephone = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _adresse = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    public bool PeutGerer => _session.Possede(CodesPermissions.GererClients);

    public override async Task ChargerAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "chargement des clients")
            .ConfigureAwait(true);

    private async Task ChargerListeAsync()
    {
        var resultats = await _clients.RechercherAsync(Recherche, InclureInactifs).ConfigureAwait(true);

        var idPrecedent = ClientSelectionne?.Id;

        Clients.Clear();
        foreach (var client in resultats)
        {
            Clients.Add(client);
        }

        ClientSelectionne = idPrecedent is null
            ? Clients.FirstOrDefault()
            : Clients.FirstOrDefault(c => c.Id == idPrecedent) ?? Clients.FirstOrDefault();
    }

    partial void OnClientSelectionneChanged(ClientDto? value)
    {
        _ = ChargerHistoriqueAsync(value?.Id);
    }

    private async Task ChargerHistoriqueAsync(int? clientId)
    {
        Historique.Clear();

        if (clientId is null)
        {
            return;
        }

        var ventes = await _clients.ObtenirHistoriqueAsync(clientId.Value).ConfigureAwait(true);

        foreach (var vente in ventes)
        {
            Historique.Add(vente);
        }
    }

    [RelayCommand]
    private async Task RechercherAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "recherche de clients")
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
        Prenom = string.Empty;
        Telephone = string.Empty;
        Email = string.Empty;
        Adresse = string.Empty;
        Notes = string.Empty;

        ModeEdition = true;
    }

    [RelayCommand]
    private void Modifier()
    {
        if (ClientSelectionne is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un client.", "Aucune sélection");
            return;
        }

        if (!VerifierDroits())
        {
            return;
        }

        IdentifiantEdite = ClientSelectionne.Id;
        Nom = ClientSelectionne.Nom;
        Prenom = ClientSelectionne.Prenom ?? string.Empty;
        Telephone = ClientSelectionne.Telephone ?? string.Empty;
        Email = ClientSelectionne.Email ?? string.Empty;
        Adresse = ClientSelectionne.Adresse ?? string.Empty;
        Notes = ClientSelectionne.Notes ?? string.Empty;

        ModeEdition = true;
    }

    [RelayCommand]
    private void AnnulerEdition() => ModeEdition = false;

    [RelayCommand]
    private async Task EnregistrerAsync()
    {
        var demande = new DemandeClient
        {
            Nom = Nom,
            Prenom = Prenom,
            Telephone = Telephone,
            Email = Email,
            Adresse = Adresse,
            Notes = Notes
        };

        var reussi = await ExecuterAsync(
            async () =>
            {
                var enregistre = IdentifiantEdite.HasValue
                    ? await _clients.ModifierAsync(IdentifiantEdite.Value, demande).ConfigureAwait(true)
                    : await _clients.CreerAsync(demande).ConfigureAwait(true);

                await ChargerListeAsync().ConfigureAwait(true);

                ClientSelectionne = Clients.FirstOrDefault(c => c.Id == enregistre.Id)
                                    ?? ClientSelectionne;
            },
            "Opération effectuée avec succès.",
            "enregistrement d'un client").ConfigureAwait(true);

        if (reussi)
        {
            ModeEdition = false;
        }
    }

    [RelayCommand]
    private async Task BasculerEtatAsync()
    {
        if (ClientSelectionne is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un client.", "Aucune sélection");
            return;
        }

        if (!VerifierDroits())
        {
            return;
        }

        var activer = !ClientSelectionne.Actif;
        var nom = ClientSelectionne.NomComplet;
        var identifiant = ClientSelectionne.Id;

        if (!Dialogue.Confirmer(
                activer
                    ? $"Réactiver le client « {nom} » ?"
                    : $"Désactiver le client « {nom} » ?" + Environment.NewLine +
                      "Son historique d'achats sera conservé.",
                "Confirmation"))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _clients.DefinirEtatAsync(identifiant, activer).ConfigureAwait(true);
                await ChargerListeAsync().ConfigureAwait(true);
            },
            activer ? "Client réactivé." : "Client désactivé.",
            "changement d'état d'un client").ConfigureAwait(true);
    }

    private bool VerifierDroits()
    {
        if (PeutGerer)
        {
            return true;
        }

        Dialogue.Avertir(
            "Vous n'avez pas l'autorisation de gérer les clients.",
            "Action non autorisée");

        return false;
    }
}
