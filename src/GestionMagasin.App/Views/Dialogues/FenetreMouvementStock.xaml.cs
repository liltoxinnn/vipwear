using System.Windows;
using GestionMagasin.App.ViewModels.Dialogues;

namespace GestionMagasin.App.Views.Dialogues;

/// <summary>Saisie d'un mouvement de stock ou d'un ajustement.</summary>
public partial class FenetreMouvementStock : Window
{
    private readonly VueModeleMouvementStock _vueModele;

    public FenetreMouvementStock(VueModeleMouvementStock vueModele)
    {
        InitializeComponent();

        _vueModele = vueModele;
        DataContext = vueModele;

        _vueModele.FermetureDemandee += SurFermetureDemandee;
    }

    /// <summary>Vue-modèle de la fenêtre, pour la préparer avant affichage.</summary>
    public VueModeleMouvementStock VueModele => _vueModele;

    private void SurFermetureDemandee(object? sender, bool valide)
    {
        _vueModele.FermetureDemandee -= SurFermetureDemandee;

        DialogResult = valide;
    }
}
