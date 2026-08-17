using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestionMagasin.App.ViewModels;
using GestionMagasin.App.Views.Dialogues;
using GestionMagasin.Application.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace GestionMagasin.App.Views;

/// <summary>État du stock et historique des mouvements.</summary>
public partial class VueStock : UserControl
{
    private VueModeleStock? _vueModele;

    public VueStock()
    {
        InitializeComponent();

        DataContextChanged += SurContexteChange;
        Unloaded += (_, _) => Detacher();
    }

    private void SurContexteChange(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detacher();

        if (DataContext is not VueModeleStock vueModele)
        {
            return;
        }

        _vueModele = vueModele;
        _vueModele.MouvementDemande += SurMouvementDemande;
    }

    private void Detacher()
    {
        if (_vueModele is null)
        {
            return;
        }

        _vueModele.MouvementDemande -= SurMouvementDemande;
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

    private async void SurMouvementDemande(object? sender, (LigneStockDto Ligne, bool EstAjustement) contexte)
    {
        var fenetre = VueProduits.Fournisseur.GetRequiredService<FenetreMouvementStock>();

        fenetre.Owner = Window.GetWindow(this);
        fenetre.VueModele.Preparer(contexte.Ligne, contexte.EstAjustement);

        if (fenetre.ShowDialog() == true && _vueModele is not null)
        {
            await _vueModele.RechargerAsync();
        }
    }
}
