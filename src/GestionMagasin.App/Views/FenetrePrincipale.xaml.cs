using System.Windows;
using System.Windows.Input;
using GestionMagasin.App.ViewModels;
using GestionMagasin.App.Views.Dialogues;
using Microsoft.Extensions.DependencyInjection;

namespace GestionMagasin.App.Views;

/// <summary>
/// Fenêtre principale : menu à gauche, écran de travail à droite.
/// Les touches de fonction reprennent l'ordre du menu pour permettre une
/// navigation sans souris, utile en caisse.
/// </summary>
public partial class FenetrePrincipale : Window
{
    private readonly VueModelePrincipale _vueModele;
    private readonly IServiceProvider _fournisseur;
    private readonly Services.IServiceNavigation _navigation;

    public FenetrePrincipale(
        VueModelePrincipale vueModele,
        Services.IServiceNavigation navigation,
        IServiceProvider fournisseur)
    {
        InitializeComponent();

        _vueModele = vueModele;
        _navigation = navigation;
        _fournisseur = fournisseur;

        DataContext = vueModele;

        _vueModele.DeconnexionDemandee += SurDeconnexion;

        Loaded += async (_, _) => await _vueModele.ChargerAsync();
        KeyDown += SurToucheEnfoncee;
    }

    /// <summary>
    /// Propose de remplacer le mot de passe livré à l'installation. Appelée
    /// juste après la première connexion.
    /// </summary>
    public void ProposerChangementMotDePasseInitial()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            MessageBox.Show(
                this,
                "Vous êtes connecté avec le mot de passe fourni à l'installation." +
                Environment.NewLine + Environment.NewLine +
                "Pour la sécurité de votre magasin, veuillez le remplacer immédiatement.",
                "Mot de passe à modifier",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            OuvrirChangementMotDePasse();
        }));
    }

    /// <summary>Ouvre la fenêtre de changement du mot de passe personnel.</summary>
    public void OuvrirChangementMotDePasse()
    {
        using var isolee = VueProduits.Fabrique.Creer<FenetreChangementMotDePasse>();

        isolee.Fenetre.Owner = this;
        isolee.Fenetre.ShowDialog();
    }

    /// <summary>
    /// Raccourcis clavier : F1 à F12 ouvrent les écrans du menu dans l'ordre,
    /// F5 recharge l'écran affiché.
    /// </summary>
    private async void SurToucheEnfoncee(object sender, KeyEventArgs e)
    {
        if (e.Key is < Key.F1 or > Key.F12)
        {
            return;
        }

        // F5 est réservé au rechargement, usage attendu par les utilisateurs.
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.Control)
        {
            await _vueModele.RafraichirCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        var raccourci = $"F{e.Key - Key.F1 + 1}";

        var entree = _vueModele.Menu.FirstOrDefault(m => m.Raccourci == raccourci);

        if (entree is not null)
        {
            await _vueModele.OuvrirCommand.ExecuteAsync(entree);
            e.Handled = true;
        }
    }

    private void SurDeconnexion(object? sender, EventArgs e)
    {
        _vueModele.DeconnexionDemandee -= SurDeconnexion;

        // La session est refermée avant d'ouvrir l'écran de connexion :
        // l'écran affiché est libéré et la vue-modèle se détache du service
        // de navigation, qui, lui, dure toute l'exécution du logiciel.
        _vueModele.Liberer();
        _navigation.Reinitialiser();

        var connexion = _fournisseur.GetRequiredService<FenetreConnexion>();

        System.Windows.Application.Current.MainWindow = connexion;
        connexion.Show();

        Close();
    }
}
