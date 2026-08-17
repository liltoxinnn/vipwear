using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels.Dialogues;

/// <summary>Ligne de commande en cours de réception.</summary>
public partial class LigneReceptionSaisie : ObservableObject
{
    public required int LigneAchatId { get; init; }

    public required string Designation { get; init; }

    public required string Sku { get; init; }

    public required int QuantiteCommandee { get; init; }

    public required int DejaRecue { get; init; }

    public int Restante => QuantiteCommandee - DejaRecue;

    [ObservableProperty]
    private int _quantiteRecue;
}

/// <summary>
/// Réception d'une commande fournisseur. Les quantités saisies augmentent le
/// stock au moment de la validation, jamais avant.
/// </summary>
public partial class VueModeleReception : VueModeleBase
{
    private readonly IServiceAchats _achats;

    public VueModeleReception(
        IServiceAchats achats,
        IServiceDialogue dialogue,
        ILogger<VueModeleReception> journal)
        : base(dialogue, journal)
    {
        _achats = achats;
    }

    public override string Titre => "Réception de marchandise";

    public ObservableCollection<LigneReceptionSaisie> Lignes { get; } = [];

    [ObservableProperty]
    private int _achatId;

    [ObservableProperty]
    private string _numeroAchat = string.Empty;

    [ObservableProperty]
    private string _fournisseur = string.Empty;

    public event EventHandler<bool>? FermetureDemandee;

    /// <summary>Charge les lignes restant à recevoir.</summary>
    public void Preparer(AchatDto achat)
    {
        AchatId = achat.Id;
        NumeroAchat = achat.NumeroAchat;
        Fournisseur = achat.FournisseurNom;

        Lignes.Clear();

        foreach (var ligne in achat.Lignes.Where(l => l.QuantiteRestante > 0))
        {
            Lignes.Add(new LigneReceptionSaisie
            {
                LigneAchatId = ligne.Id,
                Designation = ligne.Designation,
                Sku = ligne.Sku,
                QuantiteCommandee = ligne.Quantite,
                DejaRecue = ligne.QuantiteRecue,
                // La quantité restante est proposée par défaut : la réception
                // complète, cas le plus fréquent, ne demande aucune saisie.
                QuantiteRecue = ligne.QuantiteRestante
            });
        }
    }

    [RelayCommand]
    private void ToutRecevoir()
    {
        foreach (var ligne in Lignes)
        {
            ligne.QuantiteRecue = ligne.Restante;
        }
    }

    [RelayCommand]
    private void RienRecevoir()
    {
        foreach (var ligne in Lignes)
        {
            ligne.QuantiteRecue = 0;
        }
    }

    [RelayCommand]
    private async Task ValiderAsync()
    {
        var receptions = Lignes
            .Where(l => l.QuantiteRecue > 0)
            .Select(l => new LigneReception
            {
                LigneAchatId = l.LigneAchatId,
                QuantiteRecue = l.QuantiteRecue
            })
            .ToList();

        if (receptions.Count == 0)
        {
            Dialogue.Avertir(
                "Saisissez au moins une quantité reçue.",
                "Aucune quantité saisie");

            return;
        }

        var enExces = Lignes.FirstOrDefault(l => l.QuantiteRecue > l.Restante);

        if (enExces is not null)
        {
            Dialogue.Erreur(
                $"La quantité saisie pour « {enExces.Designation} » dépasse la quantité restant " +
                $"à recevoir ({enExces.Restante}).",
                "Quantité incorrecte");

            return;
        }

        if (!Dialogue.Confirmer(
                $"Réceptionner {receptions.Sum(r => r.QuantiteRecue)} article(s) " +
                $"sur la commande {NumeroAchat} ?" + Environment.NewLine +
                "Le stock sera augmenté en conséquence.",
                "Confirmer la réception"))
        {
            return;
        }

        var reussi = await ExecuterAsync(
            async () => await _achats.ReceptionnerAsync(AchatId, receptions).ConfigureAwait(true),
            "Marchandise réceptionnée. Le stock a été mis à jour.",
            "réception d'une commande").ConfigureAwait(true);

        if (reussi is not null)
        {
            FermetureDemandee?.Invoke(this, true);
        }
    }

    [RelayCommand]
    private void Annuler() => FermetureDemandee?.Invoke(this, false);
}
