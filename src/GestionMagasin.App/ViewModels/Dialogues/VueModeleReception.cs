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

    /// <summary>
    /// Prévient la fenêtre qu'une quantité a changé, pour qu'elle recompte le
    /// total annoncé.
    /// </summary>
    internal Action? SurChangement { get; set; }

    [ObservableProperty]
    private int _quantiteRecue;

    partial void OnQuantiteRecueChanged(int value) => SurChangement?.Invoke();
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

    /// <summary>
    /// Nombre d'articles que la validation ferait entrer en stock.
    ///
    /// Affiché en permanence, et non seulement dans la demande de
    /// confirmation : c'est ce compteur resté à zéro, ou monté au total de la
    /// commande, qui signale d'un coup d'œil qu'on ne réceptionne pas ce
    /// qu'on croyait.
    /// </summary>
    public int TotalRecu => Lignes.Sum(l => l.QuantiteRecue);

    /// <summary>Résumé de ce qui va être réceptionné.</summary>
    public string ResumeReception => TotalRecu switch
    {
        0 => "Aucun article saisi pour l'instant.",
        1 => "1 article entrera en stock à la validation.",
        _ => $"{TotalRecu} articles entreront en stock à la validation."
    };

    private void Recompter()
    {
        OnPropertyChanged(nameof(TotalRecu));
        OnPropertyChanged(nameof(ResumeReception));
    }

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
                // Rien n'est proposé d'avance, et c'est tout l'objet de cette
                // fenêtre.
                //
                // Elle ne s'ouvre que par « Réception partielle » : la
                // réception complète a son propre bouton, qui ne passe pas
                // par ici. Pré-remplir chaque ligne avec ce qu'il reste
                // rendait donc les deux boutons identiques — le magasin
                // n'ayant reçu qu'un carton sur trois validait la commande
                // entière sans s'en apercevoir, et le stock annonçait des
                // articles qui n'étaient jamais arrivés.
                //
                // Les quantités partent de zéro : on saisit ce qui est
                // réellement sur le quai. « Tout recevoir » reste là pour
                // remplir d'un geste si finalement tout est arrivé.
                QuantiteRecue = 0,
                SurChangement = Recompter
            });
        }

        Recompter();
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
