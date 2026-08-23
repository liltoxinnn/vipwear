using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels.Dialogues;

/// <summary>Formulaire d'une déclinaison : taille, couleur, code-barres et prix.</summary>
public partial class VueModeleFormulaireVariante : VueModeleBase
{
    private readonly IServiceProduits _produits;

    public VueModeleFormulaireVariante(
        IServiceProduits produits,
        IServiceDialogue dialogue,
        ILogger<VueModeleFormulaireVariante> journal)
        : base(dialogue, journal)
    {
        _produits = produits;
    }

    public override string Titre => IdentifiantEdite.HasValue
        ? "Modifier la déclinaison"
        : "Nouvelle déclinaison";

    public ObservableCollection<ReferenceDto> Tailles { get; } = [];

    public ObservableCollection<ReferenceDto> Couleurs { get; } = [];

    [ObservableProperty]
    private int _produitId;

    [ObservableProperty]
    private string _nomProduit = string.Empty;

    [ObservableProperty]
    private int? _identifiantEdite;

    [ObservableProperty]
    private ReferenceDto? _taille;

    [ObservableProperty]
    private ReferenceDto? _couleur;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _codeBarres = string.Empty;

    [ObservableProperty]
    private string _prixAchat = string.Empty;

    [ObservableProperty]
    private string _prixVente = string.Empty;

    [ObservableProperty]
    private int _seuilMinimum;

    [ObservableProperty]
    private int _quantiteInitiale;

    /// <summary>La quantité initiale n'est proposée qu'à la création.</summary>
    public bool EstCreation => !IdentifiantEdite.HasValue;

    public event EventHandler<bool>? FermetureDemandee;

    public async Task PreparerAsync(ProduitDto produit, VarianteDto? variante)
    {
        await ExecuterAsync(async () =>
        {
            // Seules les tailles du système de la famille : proposer « XXL »
            // sur une chaussure laisserait créer une déclinaison invendable,
            // que le stock garderait ensuite indéfiniment.
            Remplir(Tailles, await _produits
                .ListerTaillesAsync(systemeTailleId: produit.SystemeTailleId)
                .ConfigureAwait(true));
            Remplir(Couleurs, await _produits.ListerCouleursAsync().ConfigureAwait(true));

            ProduitId = produit.Id;
            NomProduit = produit.Nom;

            if (variante is null)
            {
                IdentifiantEdite = null;
                Taille = Tailles.FirstOrDefault();
                Couleur = Couleurs.FirstOrDefault();
                Sku = string.Empty;
                CodeBarres = string.Empty;
                PrixAchat = string.Empty;
                PrixVente = string.Empty;
                SeuilMinimum = 0;
                QuantiteInitiale = 0;
            }
            else
            {
                IdentifiantEdite = variante.Id;
                Taille = Tailles.FirstOrDefault(t => t.Id == variante.TailleId);
                Couleur = Couleurs.FirstOrDefault(c => c.Id == variante.CouleurId);
                Sku = variante.Sku;
                CodeBarres = variante.CodeBarres ?? string.Empty;
                PrixAchat = variante.PrixAchatSpecifique?.ToString("N2") ?? string.Empty;
                PrixVente = variante.PrixVenteSpecifique?.ToString("N2") ?? string.Empty;
                SeuilMinimum = variante.SeuilMinimum;
                QuantiteInitiale = 0;
            }

            OnPropertyChanged(nameof(Titre));
            OnPropertyChanged(nameof(EstCreation));
        }, contexteJournal: "préparation du formulaire de déclinaison").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task EnregistrerAsync()
    {
        if (Taille is null || Couleur is null)
        {
            Dialogue.Avertir("Sélectionnez une taille et une couleur.", "Champs obligatoires");
            return;
        }

        var demande = new DemandeVariante
        {
            TailleId = Taille.Id,
            CouleurId = Couleur.Id,
            Sku = string.IsNullOrWhiteSpace(Sku) ? null : Sku,
            CodeBarres = CodeBarres,
            PrixAchat = AnalyserMontant(PrixAchat),
            PrixVente = AnalyserMontant(PrixVente),
            SeuilMinimum = SeuilMinimum,
            QuantiteInitiale = QuantiteInitiale
        };

        var reussi = await ExecuterAsync(
            async () =>
            {
                if (IdentifiantEdite.HasValue)
                {
                    await _produits.ModifierVarianteAsync(IdentifiantEdite.Value, demande).ConfigureAwait(true);
                }
                else
                {
                    await _produits.AjouterVarianteAsync(ProduitId, demande).ConfigureAwait(true);
                }
            },
            "Déclinaison enregistrée avec succès.",
            "enregistrement d'une déclinaison").ConfigureAwait(true);

        if (reussi)
        {
            FermetureDemandee?.Invoke(this, true);
        }
    }

    [RelayCommand]
    private void Annuler() => FermetureDemandee?.Invoke(this, false);

    /// <summary>
    /// Un champ de prix laissé vide signifie « utiliser le prix du produit ».
    /// </summary>
    private static decimal? AnalyserMontant(string saisie)
    {
        if (string.IsNullOrWhiteSpace(saisie))
        {
            return null;
        }

        return decimal.TryParse(
            saisie,
            System.Globalization.NumberStyles.Any,
            Application.Common.FormatageMontant.CultureApplication,
            out var montant)
            ? montant
            : null;
    }

    private static void Remplir<T>(ObservableCollection<T> destination, IReadOnlyList<T> source)
    {
        destination.Clear();

        foreach (var element in source)
        {
            destination.Add(element);
        }
    }
}
