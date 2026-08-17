using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestionMagasin.App.ViewModels;
using GestionMagasin.App.Views.Dialogues;
using GestionMagasin.Application.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace GestionMagasin.App.Views;

/// <summary>Commandes fournisseurs et réceptions.</summary>
public partial class VueAchats : UserControl
{
    private VueModeleAchats? _vueModele;

    public VueAchats()
    {
        InitializeComponent();

        DataContextChanged += SurContexteChange;
        Unloaded += (_, _) => Detacher();
    }

    private void SurContexteChange(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detacher();

        if (DataContext is not VueModeleAchats vueModele)
        {
            return;
        }

        _vueModele = vueModele;
        _vueModele.FormulaireAchatDemande += SurFormulaireAchat;
        _vueModele.ReceptionDemandee += SurReception;
    }

    private void Detacher()
    {
        if (_vueModele is null)
        {
            return;
        }

        _vueModele.FormulaireAchatDemande -= SurFormulaireAchat;
        _vueModele.ReceptionDemandee -= SurReception;
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

    private async void SurFormulaireAchat(object? sender, EventArgs e)
    {
        using var isolee = VueProduits.Fabrique.Creer<FenetreAchat>();
        var fenetre = isolee.Fenetre;

        fenetre.Owner = Window.GetWindow(this);

        await fenetre.VueModele.PreparerAsync();

        if (fenetre.ShowDialog() == true && _vueModele is not null)
        {
            await _vueModele.RechargerAsync();
        }
    }

    private async void SurReception(object? sender, AchatDto achat)
    {
        using var isolee = VueProduits.Fabrique.Creer<FenetreReception>();
        var fenetre = isolee.Fenetre;

        fenetre.Owner = Window.GetWindow(this);
        fenetre.VueModele.Preparer(achat);

        if (fenetre.ShowDialog() == true && _vueModele is not null)
        {
            await _vueModele.RechargerAsync();
        }
    }
}
