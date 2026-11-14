using System.Windows;
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

    private void SurFermetureDemandee(object? sender, bool valide)
    {
        _vueModele.FermetureDemandee -= SurFermetureDemandee;

        DialogResult = valide;
    }
}
