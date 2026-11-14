using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestionMagasin.App.ViewModels.Dialogues;

namespace GestionMagasin.App.Views.Dialogues;

/// <summary>Impression des étiquettes code-barres d'un produit.</summary>
public partial class FenetreEtiquettes : Window
{
    private readonly VueModeleEtiquettes _vueModele;

    public FenetreEtiquettes(VueModeleEtiquettes vueModele)
    {
        InitializeComponent();

        _vueModele = vueModele;
        DataContext = vueModele;

        _vueModele.FermetureDemandee += SurFermetureDemandee;
    }

    /// <summary>Vue-modèle de la fenêtre, pour la préparer avant affichage.</summary>
    public VueModeleEtiquettes VueModele => _vueModele;

    /// <summary>Le nombre entier est sélectionné : taper le remplace, sans avoir à l'effacer.</summary>
    private void SurChampNombreActive(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox champ)
        {
            champ.SelectAll();
        }
    }

    /// <summary>
    /// Le premier clic donne la main au champ au lieu d'y poser le curseur :
    /// sans cela, il placerait le curseur au milieu du nombre et la
    /// sélection serait aussitôt perdue.
    /// </summary>
    private void SurClicChampNombre(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox { IsKeyboardFocusWithin: false } champ)
        {
            e.Handled = true;
            champ.Focus();
        }
    }

    private void SurFermetureDemandee(object? sender, bool valide)
    {
        _vueModele.FermetureDemandee -= SurFermetureDemandee;

        DialogResult = valide;
    }
}
