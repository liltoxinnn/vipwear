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

    /// <summary>
    /// Toutes les marques, y compris celles mises en sommeil. La liste
    /// déroulante n'en montre que les actives, mais une marque désactivée
    /// reste une marque : retaper son nom doit la retrouver, et non refuser
    /// l'enregistrement pour cause de doublon.
    /// </summary>
    private IReadOnlyList<ReferenceDto> _toutesMarques = [];

    public VueModeleFormulaireProduit(
        IServiceProduits produits,
        IServiceDialogue dialogue,
        ILogger<VueModeleFormulaireProduit> journal)
        : base(dialogue, journal)
    {
        _produits = produits;
    }

    public override string Titre => IdentifiantEdite.HasValue ? "Modifier le produit" : "Nouveau produit";

    /// <summary>
    /// Familles d'articles. Chacune impose son système de tailles : c'est ce
    /// choix qui déterminera les tailles proposées aux déclinaisons.
    /// </summary>
    public ObservableCollection<CategorieDto> Familles { get; } = [];

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
    private CategorieDto? _famille;

    /// <summary>
    /// Marque, saisie ou choisie. Le champ est ouvert à l'écriture : un
    /// magasin reçoit une marque nouvelle avant d'avoir songé à la déclarer,
    /// et l'obliger à passer par un autre écran au moment où il range son
    /// arrivage n'aurait servi à rien. Un nom inconnu est créé à
    /// l'enregistrement du produit.
    /// </summary>
    [ObservableProperty]
    private string _nomMarque = string.Empty;

    [ObservableProperty]
    private string _collection = string.Empty;

    [ObservableProperty]
    private string _saison = string.Empty;

    [ObservableProperty]
    private decimal _prixAchat;

    [ObservableProperty]
    private decimal _prixVente;

    /// <summary>
    /// Rappel du système de tailles de la famille choisie, affiché sous la
    /// liste : le vendeur voit tout de suite qu'un pantalon se déclinera en
    /// 38, 40, 42 et non en S, M, L.
    /// </summary>
    public string SystemeTaille => Famille is null
        ? "Choisissez une famille pour connaître les tailles proposées."
        : $"Tailles proposées : {Famille.SystemeTaille}";

    partial void OnFamilleChanged(CategorieDto? value) => OnPropertyChanged(nameof(SystemeTaille));

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
            Remplir(Familles, await _produits.ListerCategoriesAsync().ConfigureAwait(true));
            _toutesMarques = await _produits.ListerMarquesAsync(inclureInactifs: true).ConfigureAwait(true);
            Remplir(Marques, _toutesMarques.Where(m => m.Actif).ToList());
            Remplir(Collections, await _produits.ListerCollectionsAsync().ConfigureAwait(true));
            Remplir(Saisons, await _produits.ListerSaisonsAsync().ConfigureAwait(true));

            if (produit is null)
            {
                IdentifiantEdite = null;
                Reference = string.Empty;
                Sku = string.Empty;
                Nom = string.Empty;
                Description = string.Empty;
                Famille = null;
                NomMarque = string.Empty;
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
                Famille = Familles.FirstOrDefault(f => f.Id == produit.CategorieId);
                NomMarque = _toutesMarques.FirstOrDefault(m => m.Id == produit.MarqueId)?.Nom ?? string.Empty;
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
        // Contrôlé ici plutôt que laissé au service : le message arrive avant
        // l'aller-retour, et désigne le champ à remplir.
        if (Famille is null)
        {
            Dialogue.Avertir(
                "Choisissez la famille de l'article. C'est elle qui détermine " +
                "les tailles proposées : lettres pour une chemise, pointures " +
                "pour une chaussure.",
                "Famille manquante");

            return;
        }

        var reussi = await ExecuterAsync(
            async () =>
            {
                // La marque est réglée avant le produit : si elle est
                // nouvelle, elle doit exister au moment où le produit s'y
                // rattache. L'échec éventuel remonte ici et interrompt
                // l'enregistrement, plutôt que d'enregistrer le produit sans
                // sa marque.
                var marqueId = await ResoudreMarqueAsync().ConfigureAwait(true);

                var demande = new DemandeProduit
                {
                    Reference = Reference,
                    Sku = Sku,
                    Nom = Nom,
                    Description = Description,
                    MarqueId = marqueId,
                    CategorieId = Famille.Id,
                    Collection = Collection,
                    Saison = Saison,
                    PrixAchat = PrixAchat,
                    PrixVente = PrixVente
                };

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

    /// <summary>
    /// Retrouve la marque saisie, ou la crée si le nom est nouveau.
    /// </summary>
    /// <returns>
    /// L'identifiant de la marque, ou <c>null</c> si le champ a été laissé
    /// vide : la marque reste facultative.
    /// </returns>
    private async Task<int?> ResoudreMarqueAsync()
    {
        var nom = NomMarque?.Trim();

        if (string.IsNullOrEmpty(nom))
        {
            return null;
        }

        // La comparaison ignore la casse : « Lacoste » et « lacoste » sont la
        // même marque, et en créer deux fausserait tous les filtres.
        var connue = _toutesMarques.FirstOrDefault(
            m => string.Equals(m.Nom, nom, StringComparison.CurrentCultureIgnoreCase));

        if (connue is not null)
        {
            return connue.Id;
        }

        var creee = await _produits.EnregistrerMarqueAsync(null, nom, null).ConfigureAwait(true);

        // La nouvelle marque rejoint les deux listes : le formulaire peut
        // être réenregistré sans la recréer, et la liste déroulante la
        // propose immédiatement.
        _toutesMarques = [.. _toutesMarques, creee];
        Marques.Add(creee);

        return creee.Id;
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
