using System.Windows;
using GestionMagasin.App.ViewModels.Dialogues;

namespace GestionMagasin.App.Views.Dialogues;

/// <summary>Formulaire de création ou de modification d'un produit.</summary>
public partial class FenetreProduit : Window
{
    private readonly VueModeleFormulaireProduit _vueModele;

    public FenetreProduit(VueModeleFormulaireProduit vueModele)
    {
        InitializeComponent();

        _vueModele = vueModele;
        DataContext = vueModele;

        _vueModele.FermetureDemandee += SurFermetureDemandee;
    }

    /// <summary>Vue-modèle de la fenêtre, pour la préparer avant affichage.</summary>
    public VueModeleFormulaireProduit VueModele => _vueModele;

    private void SurFermetureDemandee(object? sender, bool valide)
    {
        _vueModele.FermetureDemandee -= SurFermetureDemandee;

        DialogResult = valide;
    }
}
