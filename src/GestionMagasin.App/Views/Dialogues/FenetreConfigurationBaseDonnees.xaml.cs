using System.Windows;
using System.Windows.Media;
using GestionMagasin.App.Services;
using GestionMagasin.Infrastructure.Services;

namespace GestionMagasin.App.Views.Dialogues;

/// <summary>
/// Fenêtre affichée lorsque le logiciel ne parvient pas à joindre la base de
/// données : à la première installation, ou si le serveur a changé.
///
/// Elle évite au magasin d'avoir à modifier un fichier de configuration à la
/// main. La connexion doit être testée avec succès avant de pouvoir démarrer.
/// </summary>
public partial class FenetreConfigurationBaseDonnees : Window
{
    /// <summary>Chaîne de connexion validée, disponible après fermeture.</summary>
    public string? ChaineConnexion { get; private set; }

    public FenetreConfigurationBaseDonnees(string? chaineActuelle = null)
    {
        InitializeComponent();

        Precharger(chaineActuelle);

        Loaded += (_, _) => ChampMotDePasse.Focus();
    }

    /// <summary>Reprend les valeurs déjà configurées pour éviter une ressaisie complète.</summary>
    private void Precharger(string? chaineActuelle)
    {
        if (string.IsNullOrWhiteSpace(chaineActuelle))
        {
            return;
        }

        try
        {
            var constructeur = new Npgsql.NpgsqlConnectionStringBuilder(chaineActuelle);

            if (!string.IsNullOrWhiteSpace(constructeur.Host))
            {
                ChampServeur.Text = constructeur.Host;
            }

            if (constructeur.Port > 0)
            {
                ChampPort.Text = constructeur.Port.ToString();
            }

            if (!string.IsNullOrWhiteSpace(constructeur.Database))
            {
                ChampBase.Text = constructeur.Database;
            }

            if (!string.IsNullOrWhiteSpace(constructeur.Username))
            {
                ChampUtilisateur.Text = constructeur.Username;
            }
        }
        catch (Exception)
        {
            // Chaîne illisible : les valeurs par défaut du formulaire restent.
        }
    }

    private async void SurTester(object sender, RoutedEventArgs e) =>
        await TesterAsync();

    private async void SurDemarrer(object sender, RoutedEventArgs e)
    {
        // Le bouton n'est actif qu'après un test réussi, mais les valeurs ont
        // pu changer depuis : on revérifie avant d'enregistrer.
        if (!await TesterAsync())
        {
            return;
        }

        try
        {
            ConfigurationConnexion.Enregistrer(ChaineConnexion!);
        }
        catch (Exception)
        {
            AfficherMessage(
                "La configuration n'a pas pu être enregistrée." + Environment.NewLine +
                $"Vérifiez que le dossier « {AppContext.BaseDirectory} » est accessible en écriture.",
                succes: false);

            return;
        }

        DialogResult = true;
    }

    private void SurQuitter(object sender, RoutedEventArgs e) => DialogResult = false;

    private async Task<bool> TesterAsync()
    {
        if (!int.TryParse(ChampPort.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            AfficherMessage("Le port doit être un nombre compris entre 1 et 65535.", succes: false);
            return false;
        }

        if (string.IsNullOrWhiteSpace(ChampServeur.Text)
            || string.IsNullOrWhiteSpace(ChampBase.Text)
            || string.IsNullOrWhiteSpace(ChampUtilisateur.Text))
        {
            AfficherMessage("Veuillez remplir tous les champs.", succes: false);
            return false;
        }

        BasculerAttente(true);

        try
        {
            var chaine = TesteurConnexion.ComposerChaine(
                ChampServeur.Text,
                port,
                ChampBase.Text,
                ChampUtilisateur.Text,
                ChampMotDePasse.Password);

            var resultat = await TesteurConnexion.VerifierEtPreparerAsync(chaine);

            AfficherMessage(resultat.Message, resultat.Reussie);

            ChaineConnexion = resultat.Reussie ? chaine : null;
            BoutonDemarrer.IsEnabled = resultat.Reussie;

            return resultat.Reussie;
        }
        finally
        {
            BasculerAttente(false);
        }
    }

    private void BasculerAttente(bool enCours)
    {
        BoutonTester.IsEnabled = !enCours;
        BoutonTester.Content = enCours ? "Test en cours…" : "Tester la connexion";
    }

    private void AfficherMessage(string message, bool succes)
    {
        TexteMessage.Text = message;

        var cle = succes ? "PinceauSucces" : "PinceauDanger";
        var fond = succes ? "PinceauFondSucces" : "PinceauFondDanger";

        TexteMessage.Foreground = (Brush)FindResource(cle);
        ZoneMessage.Background = (Brush)FindResource(fond);
        ZoneMessage.Visibility = Visibility.Visible;
    }
}
