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

        // Le curseur se place sur la quantité, texte sélectionné : la
        // fenêtre s'ouvre parce qu'on veut corriger un nombre, et taper le
        // nouveau doit suffire. Sans cela il fallait viser le champ à la
        // souris, puis effacer l'ancienne valeur.
        Loaded += (_, _) =>
        {
            var champ = _vueModele.EstAjustement
                ? ChampQuantiteConstatee
                : ChampQuantiteMouvement;

            champ.Focus();
            champ.SelectAll();
        };
    }

    /// <summary>Vue-modèle de la fenêtre, pour la préparer avant affichage.</summary>
    public VueModeleMouvementStock VueModele => _vueModele;

    private void SurFermetureDemandee(object? sender, bool valide)
    {
        _vueModele.FermetureDemandee -= SurFermetureDemandee;

        DialogResult = valide;
    }
}
