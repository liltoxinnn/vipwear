using System.Windows;
using System.Windows.Input;
using GestionMagasin.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GestionMagasin.App.Views;

/// <summary>
/// Fenêtre de connexion. Le mot de passe transite par un PasswordBox : il
/// n'est jamais lié à une propriété exposée, conformément aux pratiques de
/// sécurité de WPF.
/// </summary>
public partial class FenetreConnexion : Window
{
    private readonly VueModeleConnexion _vueModele;
    private readonly IServiceProvider _fournisseur;

    public FenetreConnexion(VueModeleConnexion vueModele, IServiceProvider fournisseur)
    {
        InitializeComponent();

        _vueModele = vueModele;
        _fournisseur = fournisseur;

        DataContext = vueModele;

        _vueModele.ConnexionReussie += SurConnexionReussie;

        Loaded += async (_, _) =>
        {
            await _vueModele.ChargerAsync();
            ChampMotDePasse.Focus();
        };
    }

    private void SurMotDePasseModifie(object sender, RoutedEventArgs e) =>
        _vueModele.MotDePasse = ChampMotDePasse.Password;

    private void SurToucheMotDePasse(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vueModele.SeConnecterCommand.CanExecute(null))
        {
            _vueModele.SeConnecterCommand.Execute(null);
        }
    }

    private void SurConnexionReussie(object? sender, EventArgs e)
    {
        ChampMotDePasse.Clear();

        var principale = _fournisseur.GetRequiredService<FenetrePrincipale>();

        System.Windows.Application.Current.MainWindow = principale;
        principale.Show();

        // Le changement du mot de passe initial est proposé dès l'ouverture,
        // avant toute utilisation du logiciel.
        if (_vueModele.MotDePasseAChanger)
        {
            principale.ProposerChangementMotDePasseInitial();
        }

        _vueModele.ConnexionReussie -= SurConnexionReussie;

        Close();
    }
}
