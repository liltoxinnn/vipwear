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

    // Aspect de la fenêtre avant le plein écran, pour le rétablir tel quel.
    private WindowState _etatAvant = WindowState.Maximized;
    private WindowStyle _styleAvant = WindowStyle.SingleBorderWindow;
    private ResizeMode _redimensionnementAvant = ResizeMode.CanResize;

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
    /// Plein écran : la fenêtre couvre l'écran entier, bordure et barre des
    /// tâches comprises.
    ///
    /// L'état vit dans la fenêtre et non dans la vue-modèle : il ne dit rien
    /// du magasin, seulement de la façon dont ce poste-ci est installé. Un
    /// poste de caisse posé sur le comptoir gagne les deux centimètres que
    /// prennent le cadre et la barre des tâches ; un poste de bureau garde sa
    /// fenêtre.
    /// </summary>
    public static readonly DependencyProperty PleinEcranProperty =
        DependencyProperty.Register(
            nameof(PleinEcran),
            typeof(bool),
            typeof(FenetrePrincipale),
            new PropertyMetadata(false, SurPleinEcranChange));

    /// <inheritdoc cref="PleinEcranProperty" />
    public bool PleinEcran
    {
        get => (bool)GetValue(PleinEcranProperty);
        set => SetValue(PleinEcranProperty, value);
    }

    private static void SurPleinEcranChange(DependencyObject cible, DependencyPropertyChangedEventArgs e) =>
        ((FenetrePrincipale)cible).AppliquerPleinEcran((bool)e.NewValue);

    private void AppliquerPleinEcran(bool actif)
    {
        if (actif)
        {
            _etatAvant = WindowState;
            _styleAvant = WindowStyle;
            _redimensionnementAvant = ResizeMode;

            // Le retour à l'état normal avant de retirer la bordure n'est pas
            // une précaution : une fenêtre déjà agrandie conserve la place
            // réservée à la barre des tâches, et le « plein écran » laisserait
            // une bande grise en bas de l'écran.
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            WindowStyle = _styleAvant;
            ResizeMode = _redimensionnementAvant;
            WindowState = _etatAvant;
        }
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
    /// Ctrl+F5 recharge l'écran affiché, Ctrl+F11 bascule en plein écran et
    /// Échap en sort.
    /// </summary>
    private async void SurToucheEnfoncee(object sender, KeyEventArgs e)
    {
        // Échap ne quitte le plein écran que si l'on y est : ailleurs, la
        // touche doit rester libre pour fermer les listes déroulantes.
        if (e.Key == Key.Escape && PleinEcran)
        {
            PleinEcran = false;
            e.Handled = true;
            return;
        }

        if (e.Key is < Key.F1 or > Key.F12)
        {
            return;
        }

        // F11 seul ouvre l'écran des comptes, comme les onze autres touches de
        // fonction. Le plein écran prend donc Ctrl+F11 : ajouter une
        // quatorzième touche aurait décalé tout le menu, dont les raccourcis
        // sont écrits en face de chaque entrée.
        if (e.Key == Key.F11 && Keyboard.Modifiers == ModifierKeys.Control)
        {
            PleinEcran = !PleinEcran;
            e.Handled = true;
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
