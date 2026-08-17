using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels.Dialogues;

/// <summary>Formulaire de création ou de modification d'un produit.</summary>
public partial class VueModeleFormulaireProduit : VueModeleBase
{
    private readonly IServiceProduits _produits;

    public VueModeleFormulaireProduit(
        IServiceProduits produits,
        IServiceDialogue dialogue,
        ILogger<VueModeleFormulaireProduit> journal)
        : base(dialogue, journal)
    {
        _produits = produits;
    }

    public override string Titre => IdentifiantEdite.HasValue ? "Modifier le produit" : "Nouveau produit";

    public ObservableCollection<ReferenceDto> Marques { get; } = [];

    public ObservableCollection<string> Collections { get; } = [];

    public ObservableCollection<string> Saisons { get; } = [];

    [ObservableProperty]
    private int? _identifiantEdite;

    [ObservableProperty]
    private string _reference = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _nom = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private ReferenceDto? _marque;

    [ObservableProperty]
    private string _collection = string.Empty;

    [ObservableProperty]
    private string _saison = string.Empty;

    [ObservableProperty]
    private decimal _prixAchat;

    [ObservableProperty]
    private decimal _prixVente;

    /// <summary>Marge calculée à la volée pour guider la saisie du prix.</summary>
    public decimal Marge => PrixVente - PrixAchat;

    public decimal TauxMarge => PrixVente > 0
        ? Math.Round((PrixVente - PrixAchat) / PrixVente * 100m, 2)
        : 0m;

    /// <summary>Identifiant du produit enregistré, disponible après validation.</summary>
    public int? ProduitEnregistreId { get; private set; }

    /// <summary>Déclenché lorsque le formulaire doit se fermer.</summary>
    public event EventHandler<bool>? FermetureDemandee;

    partial void OnPrixAchatChanged(decimal value) => NotifierMarge();

    partial void OnPrixVenteChanged(decimal value) => NotifierMarge();

    private void NotifierMarge()
    {
        OnPropertyChanged(nameof(Marge));
        OnPropertyChanged(nameof(TauxMarge));
    }

    /// <summary>Prépare le formulaire, en création ou en modification.</summary>
    public async Task PreparerAsync(ProduitDto? produit)
    {
        await ExecuterAsync(async () =>
        {
            Remplir(Marques, await _produits.ListerMarquesAsync().ConfigureAwait(true));
            Remplir(Collections, await _produits.ListerCollectionsAsync().ConfigureAwait(true));
            Remplir(Saisons, await _produits.ListerSaisonsAsync().ConfigureAwait(true));

            if (produit is null)
            {
                IdentifiantEdite = null;
                Reference = string.Empty;
                Sku = string.Empty;
                Nom = string.Empty;
                Description = string.Empty;
                Marque = null;
                Collection = string.Empty;
                Saison = string.Empty;
                PrixAchat = 0m;
                PrixVente = 0m;
            }
            else
            {
                IdentifiantEdite = produit.Id;
                Reference = produit.Reference;
                Sku = produit.Sku;
                Nom = produit.Nom;
                Description = produit.Description ?? string.Empty;
                Marque = Marques.FirstOrDefault(m => m.Id == produit.MarqueId);
                Collection = produit.Collection ?? string.Empty;
                Saison = produit.Saison ?? string.Empty;
                PrixAchat = produit.PrixAchat;
                PrixVente = produit.PrixVente;
            }

            OnPropertyChanged(nameof(Titre));
        }, contexteJournal: "préparation du formulaire produit").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task EnregistrerAsync()
    {
        var demande = new DemandeProduit
        {
            Reference = Reference,
            Sku = Sku,
            Nom = Nom,
            Description = Description,
            MarqueId = Marque?.Id,
            Collection = Collection,
            Saison = Saison,
            PrixAchat = PrixAchat,
            PrixVente = PrixVente
        };

        var reussi = await ExecuterAsync(
            async () =>
            {
                var produit = IdentifiantEdite.HasValue
                    ? await _produits.ModifierProduitAsync(IdentifiantEdite.Value, demande).ConfigureAwait(true)
                    : await _produits.CreerProduitAsync(demande).ConfigureAwait(true);

                ProduitEnregistreId = produit.Id;
            },
            "Produit enregistré avec succès.",
            "enregistrement d'un produit").ConfigureAwait(true);

        if (reussi)
        {
            FermetureDemandee?.Invoke(this, true);
        }
    }

    [RelayCommand]
    private void Annuler() => FermetureDemandee?.Invoke(this, false);

    private static void Remplir<T>(ObservableCollection<T> destination, IReadOnlyList<T> source)
    {
        destination.Clear();

        foreach (var element in source)
        {
            destination.Add(element);
        }
    }
}
