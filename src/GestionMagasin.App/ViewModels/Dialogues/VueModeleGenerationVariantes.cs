using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels.Dialogues;

/// <summary>Élément de référence cochable dans la génération groupée.</summary>
public partial class ReferenceSelectionnable : ObservableObject
{
    public required int Id { get; init; }

    public required string Nom { get; init; }

    public string? Complement { get; init; }

    [ObservableProperty]
    private bool _selectionne;
}

/// <summary>
/// Génère d'un seul geste toutes les combinaisons de tailles et de couleurs
/// choisies. Les combinaisons déjà présentes sont ignorées.
/// </summary>
public partial class VueModeleGenerationVariantes : VueModeleBase
{
    private readonly IServiceProduits _produits;

    public VueModeleGenerationVariantes(
        IServiceProduits produits,
        IServiceDialogue dialogue,
        ILogger<VueModeleGenerationVariantes> journal)
        : base(dialogue, journal)
    {
        _produits = produits;
    }

    public override string Titre => "Générer les déclinaisons";

    public ObservableCollection<ReferenceSelectionnable> Tailles { get; } = [];

    public ObservableCollection<ReferenceSelectionnable> Couleurs { get; } = [];

    [ObservableProperty]
    private int _produitId;

    [ObservableProperty]
    private string _nomProduit = string.Empty;

    [ObservableProperty]
    private int _seuilMinimum;

    public event EventHandler<bool>? FermetureDemandee;

    public async Task PreparerAsync(ProduitDto produit)
    {
        ProduitId = produit.Id;
        NomProduit = produit.Nom;

        await ExecuterAsync(async () =>
        {
            var tailles = await _produits.ListerTaillesAsync().ConfigureAwait(true);
            var couleurs = await _produits.ListerCouleursAsync().ConfigureAwait(true);

            Tailles.Clear();
            foreach (var taille in tailles)
            {
                Tailles.Add(new ReferenceSelectionnable { Id = taille.Id, Nom = taille.Nom });
            }

            Couleurs.Clear();
            foreach (var couleur in couleurs)
            {
                Couleurs.Add(new ReferenceSelectionnable
                {
                    Id = couleur.Id,
                    Nom = couleur.Nom,
                    Complement = couleur.Complement
                });
            }
        }, contexteJournal: "préparation de la génération de déclinaisons").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task GenererAsync()
    {
        var tailles = Tailles.Where(t => t.Selectionne).Select(t => t.Id).ToList();
        var couleurs = Couleurs.Where(c => c.Selectionne).Select(c => c.Id).ToList();

        if (tailles.Count == 0 || couleurs.Count == 0)
        {
            Dialogue.Avertir(
                "Sélectionnez au moins une taille et une couleur.",
                "Sélection incomplète");

            return;
        }

        var total = tailles.Count * couleurs.Count;

        if (!Dialogue.Confirmer(
                $"Générer jusqu'à {total} déclinaison(s) pour « {NomProduit} » ?" +
                Environment.NewLine +
                "Les combinaisons déjà existantes seront ignorées.",
                "Générer les déclinaisons"))
        {
            return;
        }

        var creees = 0;

        var reussi = await ExecuterAsync(
            async () =>
            {
                creees = await _produits
                    .GenererVariantesAsync(ProduitId, tailles, couleurs, SeuilMinimum)
                    .ConfigureAwait(true);
            },
            contexteJournal: "génération de déclinaisons").ConfigureAwait(true);

        if (!reussi)
        {
            return;
        }

        Dialogue.Informer(
            creees == 0
                ? "Toutes les combinaisons sélectionnées existaient déjà : aucune déclinaison n'a été créée."
                : $"{creees} déclinaison(s) créée(s) avec succès.",
            "Génération terminée");

        FermetureDemandee?.Invoke(this, true);
    }

    [RelayCommand]
    private void ToutSelectionner()
    {
        foreach (var taille in Tailles)
        {
            taille.Selectionne = true;
        }

        foreach (var couleur in Couleurs)
        {
            couleur.Selectionne = true;
        }
    }

    [RelayCommand]
    private void ToutDeselectionner()
    {
        foreach (var taille in Tailles)
        {
            taille.Selectionne = false;
        }

        foreach (var couleur in Couleurs)
        {
            couleur.Selectionne = false;
        }
    }

    [RelayCommand]
    private void Annuler() => FermetureDemandee?.Invoke(this, false);
}
