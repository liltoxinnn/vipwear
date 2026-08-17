using System.Windows.Controls;
using System.Windows.Input;
using GestionMagasin.App.ViewModels;

namespace GestionMagasin.App.Views;

/// <summary>Retours clients : saisie et historique.</summary>
public partial class VueRetours : UserControl
{
    public VueRetours() => InitializeComponent();

    private VueModeleRetours? VueModele => DataContext as VueModeleRetours;

    private async void SurToucheRecherche(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || VueModele is null)
        {
            return;
        }

        e.Handled = true;

        await VueModele.RechercherCommand.ExecuteAsync(null);
    }

    /// <summary>La touche Entrée dans le champ du numéro charge la vente à retourner.</summary>
    private async void SurToucheNumeroVente(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || VueModele is null)
        {
            return;
        }

        e.Handled = true;

        await VueModele.ChercherVenteCommand.ExecuteAsync(null);
    }
}
