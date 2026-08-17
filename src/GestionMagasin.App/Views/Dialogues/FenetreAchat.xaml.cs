using System.Windows;
using System.Windows.Input;
using GestionMagasin.App.ViewModels.Dialogues;

namespace GestionMagasin.App.Views.Dialogues;

/// <summary>Saisie d'une commande fournisseur.</summary>
public partial class FenetreAchat : Window
{
    private readonly VueModeleFormulaireAchat _vueModele;

    public FenetreAchat(VueModeleFormulaireAchat vueModele)
    {
        InitializeComponent();

        _vueModele = vueModele;
        DataContext = vueModele;

        _vueModele.FermetureDemandee += SurFermetureDemandee;
    }

    public VueModeleFormulaireAchat VueModele => _vueModele;

    private async void SurToucheRecherche(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;

        await _vueModele.RechercherCommand.ExecuteAsync(null);
    }

    private void SurFermetureDemandee(object? sender, bool valide)
    {
        _vueModele.FermetureDemandee -= SurFermetureDemandee;

        DialogResult = valide;
    }
}
