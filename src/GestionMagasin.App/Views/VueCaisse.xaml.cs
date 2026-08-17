using System.Windows.Controls;
using System.Windows.Input;
using GestionMagasin.App.ViewModels;

namespace GestionMagasin.App.Views;

/// <summary>
/// Écran de caisse. Le champ de scan reprend le focus après chaque article
/// ajouté, ce qui permet d'enchaîner les articles à la douchette sans souris.
/// </summary>
public partial class VueCaisse : UserControl
{
    public VueCaisse()
    {
        InitializeComponent();

        Loaded += (_, _) => ChampScan.Focus();
    }

    private VueModeleCaisse? VueModele => DataContext as VueModeleCaisse;

    private async void SurToucheScan(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || VueModele is null)
        {
            return;
        }

        e.Handled = true;

        await VueModele.ScannerCommand.ExecuteAsync(null);

        ChampScan.Focus();
    }

    private async void SurToucheRecherche(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || VueModele is null)
        {
            return;
        }

        e.Handled = true;

        await VueModele.RechercherCommand.ExecuteAsync(null);
    }

    private void SurDoubleClicResultat(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grille
            && grille.SelectedItem is Application.DTOs.VarianteDto variante
            && VueModele is not null)
        {
            VueModele.AjouterArticleCommand.Execute(variante);

            ChampScan.Focus();
        }
    }
}
