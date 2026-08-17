using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels.Dialogues;

/// <summary>Article ajouté à une commande fournisseur en cours de saisie.</summary>
public partial class LigneCommande : ObservableObject
{
    public required int VarianteProduitId { get; init; }

    public required string Designation { get; init; }

    public required string Sku { get; init; }

    [ObservableProperty]
    private int _quantite = 1;

    [ObservableProperty]
    private decimal _prixUnitaire;

    public decimal Total => PrixUnitaire * Quantite;

    partial void OnQuantiteChanged(int value) => Notifier();

    partial void OnPrixUnitaireChanged(decimal value) => Notifier();

    private void Notifier()
    {
        OnPropertyChanged(nameof(Total));
        LigneModifiee?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? LigneModifiee;
}

/// <summary>Saisie d'une commande fournisseur.</summary>
public partial class VueModeleFormulaireAchat : VueModeleBase
{
    private readonly IServiceAchats _achats;
    private readonly IServiceFournisseurs _fournisseurs;
    private readonly IServiceProduits _produits;

    public VueModeleFormulaireAchat(
        IServiceAchats achats,
        IServiceFournisseurs fournisseurs,
        IServiceProduits produits,
        IServiceDialogue dialogue,
        ILogger<VueModeleFormulaireAchat> journal)
        : base(dialogue, journal)
    {
        _achats = achats;
        _fournisseurs = fournisseurs;
        _produits = produits;

        Lignes.CollectionChanged += (_, _) => RecalculerTotaux();
    }

    public override string Titre => "Nouvelle commande fournisseur";

    public ObservableCollection<FournisseurDto> Fournisseurs { get; } = [];

    public ObservableCollection<VarianteDto> Resultats { get; } = [];

    public ObservableCollection<LigneCommande> Lignes { get; } = [];

    [ObservableProperty]
    private FournisseurDto? _fournisseur;

    [ObservableProperty]
    private string _recherche = string.Empty;

    [ObservableProperty]
    private decimal _remise;

    [ObservableProperty]
    private decimal _montantPaye;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _receptionImmediate;

    [ObservableProperty]
    private decimal _sousTotal;

    [ObservableProperty]
    private decimal _total;

    public event EventHandler<bool>? FermetureDemandee;

    public async Task PreparerAsync()
    {
        await ExecuterAsync(async () =>
        {
            var liste = await _fournisseurs.RechercherAsync().ConfigureAwait(true);

            Fournisseurs.Clear();
            foreach (var fournisseur in liste)
            {
                Fournisseurs.Add(fournisseur);
            }

            Fournisseur = Fournisseurs.FirstOrDefault();

            Lignes.Clear();
            Resultats.Clear();
            Recherche = string.Empty;
            Remise = 0m;
            MontantPaye = 0m;
            Notes = string.Empty;
            ReceptionImmediate = false;

            RecalculerTotaux();
        }, contexteJournal: "préparation d'une commande fournisseur").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RechercherAsync()
    {
        if (Recherche.Trim().Length < 2)
        {
            Resultats.Clear();
            return;
        }

        await ExecuterAsync(async () =>
        {
            var resultats = await _produits.RechercherVariantesAsync(Recherche, 40).ConfigureAwait(true);

            Resultats.Clear();
            foreach (var resultat in resultats)
            {
                Resultats.Add(resultat);
            }

            if (resultats.Count == 0)
            {
                MessageStatut = $"Aucun article ne correspond à « {Recherche.Trim()} ».";
            }
        }, contexteJournal: "recherche d'articles pour une commande").ConfigureAwait(true);
    }

    [RelayCommand]
    private void AjouterArticle(VarianteDto? variante)
    {
        if (variante is null)
        {
            return;
        }

        var existante = Lignes.FirstOrDefault(l => l.VarianteProduitId == variante.Id);

        if (existante is not null)
        {
            existante.Quantite++;
            return;
        }

        var ligne = new LigneCommande
        {
            VarianteProduitId = variante.Id,
            Designation = $"{variante.Couleur} / {variante.Taille}",
            Sku = variante.Sku,
            Quantite = 1,
            PrixUnitaire = variante.PrixAchat
        };

        ligne.LigneModifiee += (_, _) => RecalculerTotaux();

        Lignes.Add(ligne);
    }

    [RelayCommand]
    private void RetirerLigne(LigneCommande? ligne)
    {
        if (ligne is not null)
        {
            Lignes.Remove(ligne);
        }
    }

    partial void OnRemiseChanged(decimal value) => RecalculerTotaux();

    private void RecalculerTotaux()
    {
        SousTotal = Lignes.Sum(l => l.Total);

        if (Remise > SousTotal)
        {
            Remise = SousTotal;
        }

        Total = SousTotal - Remise;
    }

    [RelayCommand]
    private async Task EnregistrerAsync()
    {
        if (Fournisseur is null)
        {
            Dialogue.Avertir("Sélectionnez le fournisseur de cette commande.", "Fournisseur manquant");
            return;
        }

        if (Lignes.Count == 0)
        {
            Dialogue.Avertir("Ajoutez au moins un article à la commande.", "Commande vide");
            return;
        }

        if (MontantPaye > Total)
        {
            Dialogue.Avertir(
                "Le montant versé ne peut pas dépasser le total de la commande.",
                "Montant incorrect");

            return;
        }

        var demande = new DemandeAchat
        {
            FournisseurId = Fournisseur.Id,
            Remise = Remise,
            MontantPaye = MontantPaye,
            Notes = Notes,
            ReceptionImmediate = ReceptionImmediate,
            Lignes = Lignes.Select(l => new LigneAchatDemande
            {
                VarianteProduitId = l.VarianteProduitId,
                Quantite = l.Quantite,
                PrixUnitaire = l.PrixUnitaire
            }).ToList()
        };

        var reussi = await ExecuterAsync(
            async () => await _achats.CreerAchatAsync(demande).ConfigureAwait(true),
            ReceptionImmediate
                ? "Commande enregistrée et marchandise intégrée au stock."
                : "Commande enregistrée. Le stock sera mis à jour à la réception.",
            "création d'une commande fournisseur").ConfigureAwait(true);

        if (reussi is not null)
        {
            FermetureDemandee?.Invoke(this, true);
        }
    }

    [RelayCommand]
    private void Annuler() => FermetureDemandee?.Invoke(this, false);
}
