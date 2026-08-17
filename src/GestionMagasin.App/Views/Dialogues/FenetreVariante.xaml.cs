using System.Windows;
using GestionMagasin.App.ViewModels.Dialogues;

namespace GestionMagasin.App.Views.Dialogues;

/// <summary>Formulaire d'une déclinaison de produit.</summary>
public partial class FenetreVariante : Window
{
    private readonly VueModeleFormulaireVariante _vueModele;

    public FenetreVariante(VueModeleFormulaireVariante vueModele)
    {
        InitializeComponent();

        _vueModele = vueModele;
        DataContext = vueModele;

        _vueModele.FermetureDemandee += SurFermetureDemandee;
    }

    /// <summary>Vue-modèle de la fenêtre, pour la préparer avant affichage.</summary>
    public VueModeleFormulaireVariante VueModele => _vueModele;

    private void SurFermetureDemandee(object? sender, bool valide)
    {
        _vueModele.FermetureDemandee -= SurFermetureDemandee;

        DialogResult = valide;
    }
}
