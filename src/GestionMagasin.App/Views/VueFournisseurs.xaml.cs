using System.Windows.Controls;
using System.Windows.Input;
using GestionMagasin.App.ViewModels;

namespace GestionMagasin.App.Views;

/// <summary>Fiches fournisseurs et commandes.</summary>
public partial class VueFournisseurs : UserControl
{
    public VueFournisseurs() => InitializeComponent();

    /// <summary>La touche Entrée lance la recherche, comme attendu par les utilisateurs.</summary>
    private async void SurToucheRecherche(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not VueModeleFournisseurs vueModele)
        {
            return;
        }

        e.Handled = true;

        await vueModele.RechercherCommand.ExecuteAsync(null);
    }
}
