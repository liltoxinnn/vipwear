using System.Windows;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.Views.Dialogues;

/// <summary>Changement du mot de passe de l'utilisateur connecté.</summary>
public partial class FenetreChangementMotDePasse : Window
{
    private readonly IServiceAuthentification _authentification;
    private readonly IServiceDialogue _dialogue;
    private readonly ILogger<FenetreChangementMotDePasse> _journal;

    public FenetreChangementMotDePasse(
        IServiceAuthentification authentification,
        IServiceDialogue dialogue,
        ILogger<FenetreChangementMotDePasse> journal)
    {
        InitializeComponent();

        _authentification = authentification;
        _dialogue = dialogue;
        _journal = journal;

        Loaded += (_, _) => ChampActuel.Focus();
    }

    private async void SurEnregistrer(object sender, RoutedEventArgs e)
    {
        MasquerErreur();

        var actuel = ChampActuel.Password;
        var nouveau = ChampNouveau.Password;
        var confirmation = ChampConfirmation.Password;

        if (string.IsNullOrWhiteSpace(actuel) || string.IsNullOrWhiteSpace(nouveau))
        {
            AfficherErreur("Veuillez remplir tous les champs.");
            return;
        }

        if (nouveau != confirmation)
        {
            AfficherErreur("Le nouveau mot de passe et sa confirmation ne correspondent pas.");
            return;
        }

        try
        {
            await _authentification.ChangerMotDePasseAsync(actuel, nouveau);

            // Les champs sont vidés avant fermeture : aucun mot de passe ne
            // reste en mémoire dans l'interface.
            ChampActuel.Clear();
            ChampNouveau.Clear();
            ChampConfirmation.Clear();

            _dialogue.Succes("Votre mot de passe a été modifié avec succès.");

            DialogResult = true;
        }
        catch (ExceptionMetier erreur)
        {
            AfficherErreur(erreur.Message);
        }
        catch (Exception erreur)
        {
            _journal.LogError(erreur, "Erreur lors du changement de mot de passe.");
            AfficherErreur("Une erreur est survenue. Veuillez réessayer.");
        }
    }

    private void SurAnnuler(object sender, RoutedEventArgs e) => DialogResult = false;

    private void AfficherErreur(string message)
    {
        TexteErreur.Text = message;
        ZoneErreur.Visibility = Visibility.Visible;
    }

    private void MasquerErreur() => ZoneErreur.Visibility = Visibility.Collapsed;
}
