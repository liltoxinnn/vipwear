using System.Windows;
using System.Windows.Input;

namespace GestionMagasin.App.Views.Dialogues;

/// <summary>Boîte de saisie d'un texte court : motif d'annulation, note...</summary>
public partial class FenetreSaisieTexte : Window
{
    public FenetreSaisieTexte(string titre, string message, string valeurInitiale)
    {
        InitializeComponent();

        Title = titre;
        TexteMessage.Text = message;
        ChampSaisie.Text = valeurInitiale;

        Loaded += (_, _) =>
        {
            ChampSaisie.Focus();
            ChampSaisie.SelectAll();
        };
    }

    /// <summary>Texte saisi, ou chaîne vide si la fenêtre a été annulée.</summary>
    public string Valeur { get; private set; } = string.Empty;

    private void SurValider(object sender, RoutedEventArgs e)
    {
        Valeur = ChampSaisie.Text.Trim();
        DialogResult = true;
    }

    private void SurAnnuler(object sender, RoutedEventArgs e) => DialogResult = false;

    private void SurToucheEnfoncee(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SurValider(sender, e);
        }
    }
}
