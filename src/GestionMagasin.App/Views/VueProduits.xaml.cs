using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestionMagasin.App.Services;
using GestionMagasin.App.ViewModels;
using GestionMagasin.App.Views.Dialogues;
using GestionMagasin.Application.DTOs;

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
        using var isolee = Fabrique.Creer<FenetreProduit>();
        var fenetre = isolee.Fenetre;

        fenetre.Owner = Window.GetWindow(this);

        await fenetre.VueModele.PreparerAsync(produit);

        if (fenetre.ShowDialog() == true && _vueModele is not null)
        {
            await _vueModele.RechargerAsync(fenetre.VueModele.ProduitEnregistreId);
        }
    }

    private async void SurFormulaireVariante(object? sender, (ProduitDto Produit, VarianteDto? Variante) contexte)
    {
        using var isolee = Fabrique.Creer<FenetreVariante>();
        var fenetre = isolee.Fenetre;

        fenetre.Owner = Window.GetWindow(this);

        await fenetre.VueModele.PreparerAsync(contexte.Produit, contexte.Variante);

        if (fenetre.ShowDialog() == true && _vueModele is not null)
        {
            await _vueModele.RechargerAsync(contexte.Produit.Id);
        }
    }

    private async void SurGenerationVariantes(object? sender, ProduitDto produit)
    {
        using var isolee = Fabrique.Creer<FenetreGenerationVariantes>();
        var fenetre = isolee.Fenetre;

        fenetre.Owner = Window.GetWindow(this);

        await fenetre.VueModele.PreparerAsync(produit);

        if (fenetre.ShowDialog() == true && _vueModele is not null)
        {
            await _vueModele.RechargerAsync(produit.Id);
        }
    }

    /// <summary>Fabrique des fenêtres de saisie, renseignée au démarrage.</summary>
    internal static IFabriqueFenetres Fabrique { get; set; } = null!;
}
