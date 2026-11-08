using System.Windows;
using System.Windows.Controls;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Impression;

namespace GestionMagasin.App.Views.Dialogues;

/// <summary>
/// Choix de l'imprimante de tickets, au premier ticket d'un poste, ou
/// quand l'imprimante retenue ne peut pas imprimer de ticket.
///
/// L'imprimante qui s'annonce comme une imprimante de caisse est
/// présélectionnée, avec la largeur que suggère son nom : dans le cas
/// courant, il n'y a qu'à appuyer sur Imprimer.
/// </summary>
public partial class FenetreChoixImprimante : Window
{
    /// <param name="imprimantes">Imprimantes proposées, sans les imprimantes virtuelles.</param>
    /// <param name="avertissement">
    /// Pourquoi la question est posée de nouveau — l'imprimante retenue
    /// fabriquait des PDF vides, par exemple —, affiché en tête de fenêtre.
    /// </param>
    public FenetreChoixImprimante(IReadOnlyList<string> imprimantes, string? avertissement = null)
    {
        InitializeComponent();

        TexteAvertissement.Text = avertissement ?? string.Empty;
        TexteAvertissement.Visibility = string.IsNullOrWhiteSpace(avertissement)
            ? Visibility.Collapsed
            : Visibility.Visible;

        ListeImprimantes.ItemsSource = imprimantes;

        var proposee = PropositionImprimante.Proposer(imprimantes);

        if (proposee is not null)
        {
            ListeImprimantes.SelectedItem = proposee.Nom;
        }
        else if (imprimantes.Count > 0)
        {
            // Aucune ne s'annonce : rien n'est présélectionné, pour qu'aucun
            // ticket ne parte par mégarde sur l'imprimante du bureau.
            ListeImprimantes.SelectedItem = null;
        }

        TexteAucune.Visibility = imprimantes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        MettreAJourBouton();
    }

    /// <summary>Décision prise par l'utilisateur.</summary>
    public ChoixImprimanteTicket Choix { get; private set; } = new(SuiteChoixImprimante.Annuler);

    private void SurImprimanteChoisie(object sender, SelectionChangedEventArgs e)
    {
        if (ListeImprimantes.SelectedItem is string nom)
        {
            var largeur = PropositionImprimante.LargeurSupposee(nom);

            Choix80.IsChecked = largeur == LargeurRouleau.Rouleau80;
            Choix58.IsChecked = largeur == LargeurRouleau.Rouleau58;
        }

        MettreAJourBouton();
    }

    private void MettreAJourBouton() =>
        BoutonImprimer.IsEnabled = ListeImprimantes.SelectedItem is string;

    private void SurImprimer(object sender, RoutedEventArgs e)
    {
        if (ListeImprimantes.SelectedItem is not string nom)
        {
            return;
        }

        Choix = new ChoixImprimanteTicket(
            SuiteChoixImprimante.Imprimer,
            nom,
            Choix80.IsChecked == true ? LargeurRouleau.Rouleau80 : LargeurRouleau.Rouleau58);

        DialogResult = true;
    }

    private void SurPdf(object sender, RoutedEventArgs e)
    {
        Choix = new ChoixImprimanteTicket(SuiteChoixImprimante.Pdf);
        DialogResult = true;
    }

    private void SurAnnuler(object sender, RoutedEventArgs e)
    {
        Choix = new ChoixImprimanteTicket(SuiteChoixImprimante.Annuler);
        DialogResult = false;
    }
}
