using System.Windows.Controls;
using System.Windows.Input;
using GestionMagasin.App.ViewModels;

namespace GestionMagasin.App.Views;

/// <summary>État du stock et historique des mouvements.</summary>
public partial class VueStock : UserControl
{
    public VueStock() => InitializeComponent();

    /// <summary>La touche Entrée lance la recherche, comme attendu par les utilisateurs.</summary>
    private async void SurToucheRecherche(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not VueModeleStock vueModele)
        {
            return;
        }

        e.Handled = true;

        await vueModele.RechercherCommand.ExecuteAsync(null);
    }
}
