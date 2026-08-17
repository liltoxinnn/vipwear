using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestionMagasin.App.ViewModels;
using GestionMagasin.App.Views.Dialogues;
using GestionMagasin.Application.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace GestionMagasin.App.Views;

/// <summary>
/// Catalogue des produits. La vue-modèle demande l'ouverture des formulaires
/// par événement : elle n'a ainsi aucune connaissance des fenêtres WPF.
/// </summary>
public partial class VueProduits : UserControl
{
    private VueModeleProduits? _vueModele;

    public VueProduits()
    {
        InitializeComponent();

        DataContextChanged += SurContexteChange;
        Unloaded += (_, _) => Detacher();
    }

    private void SurContexteChange(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detacher();

        if (DataContext is not VueModeleProduits vueModele)
        {
            return;
        }

        _vueModele = vueModele;
        _vueModele.FormulaireProduitDemande += SurFormulaireProduit;
        _vueModele.FormulaireVarianteDemande += SurFormulaireVariante;
        _vueModele.GenerationVariantesDemandee += SurGenerationVariantes;
    }

    private void Detacher()
    {
        if (_vueModele is null)
        {
            return;
        }

        _vueModele.FormulaireProduitDemande -= SurFormulaireProduit;
        _vueModele.FormulaireVarianteDemande -= SurFormulaireVariante;
        _vueModele.GenerationVariantesDemandee -= SurGenerationVariantes;
        _vueModele = null;
    }

    private async void SurToucheRecherche(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vueModele is null)
        {
            return;
        }

        e.Handled = true;

        await _vueModele.RechercherCommand.ExecuteAsync(null);
    }

    private async void SurFormulaireProduit(object? sender, ProduitDto? produit)
    {
        var fenetre = Fournisseur.GetRequiredService<FenetreProduit>();

        fenetre.Owner = Window.GetWindow(this);

        await fenetre.VueModele.PreparerAsync(produit);

        if (fenetre.ShowDialog() == true && _vueModele is not null)
        {
            await _vueModele.RechargerAsync(fenetre.VueModele.ProduitEnregistreId);
        }
    }

    private async void SurFormulaireVariante(object? sender, (ProduitDto Produit, VarianteDto? Variante) contexte)
    {
        var fenetre = Fournisseur.GetRequiredService<FenetreVariante>();

        fenetre.Owner = Window.GetWindow(this);

        await fenetre.VueModele.PreparerAsync(contexte.Produit, contexte.Variante);

        if (fenetre.ShowDialog() == true && _vueModele is not null)
        {
            await _vueModele.RechargerAsync(contexte.Produit.Id);
        }
    }

    private async void SurGenerationVariantes(object? sender, ProduitDto produit)
    {
        var fenetre = Fournisseur.GetRequiredService<FenetreGenerationVariantes>();

        fenetre.Owner = Window.GetWindow(this);

        await fenetre.VueModele.PreparerAsync(produit);

        if (fenetre.ShowDialog() == true && _vueModele is not null)
        {
            await _vueModele.RechargerAsync(produit.Id);
        }
    }

    /// <summary>Conteneur de services de l'application, renseigné au démarrage.</summary>
    internal static IServiceProvider Fournisseur { get; set; } = null!;
}
