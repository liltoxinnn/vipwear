using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Securite;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>Écran de connexion.</summary>
public partial class VueModeleConnexion : VueModeleBase
{
    private readonly IServiceAuthentification _authentification;
    private readonly IServiceParametres _parametres;

    public VueModeleConnexion(
        IServiceAuthentification authentification,
        IServiceParametres parametres,
        IServiceDialogue dialogue,
        ILogger<VueModeleConnexion> journal)
        : base(dialogue, journal)
    {
        _authentification = authentification;
        _parametres = parametres;
    }

    public override string Titre => "Connexion";

    [ObservableProperty]
    private string _nomUtilisateur = string.Empty;

    [ObservableProperty]
    private string _motDePasse = string.Empty;

    [ObservableProperty]
    private string? _messageErreur;

    [ObservableProperty]
    private string _nomMagasin = "Gestion Magasin";

    /// <summary>Vrai lorsque le mot de passe initial doit encore être changé.</summary>
    [ObservableProperty]
    private bool _motDePasseAChanger;

    /// <summary>Déclenché après une authentification réussie.</summary>
    public event EventHandler? ConnexionReussie;

    public override async Task ChargerAsync()
    {
        var magasin = await _parametres.ObtenirAsync().ConfigureAwait(true);

        NomMagasin = magasin.NomMagasin;

        // À la toute première utilisation, l'identifiant par défaut est
        // pré-rempli pour éviter un blocage inutile.
        if (string.IsNullOrEmpty(NomUtilisateur))
        {
            NomUtilisateur = ComptesParDefaut.NomUtilisateurAdministrateur;
        }
    }

    [RelayCommand]
    private async Task SeConnecterAsync()
    {
        MessageErreur = null;

        if (string.IsNullOrWhiteSpace(NomUtilisateur) || string.IsNullOrWhiteSpace(MotDePasse))
        {
            MessageErreur = "Veuillez saisir votre identifiant et votre mot de passe.";
            return;
        }

        EstOccupe = true;

        try
        {
            var resultat = await _authentification
                .ConnecterAsync(NomUtilisateur.Trim(), MotDePasse)
                .ConfigureAwait(true);

            if (!resultat.Reussie)
            {
                MessageErreur = resultat.Message;
                MotDePasse = string.Empty;
                return;
            }

            MotDePasseAChanger = resultat.MotDePasseAChanger;
            MotDePasse = string.Empty;

            ConnexionReussie?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception erreur)
        {
            Journal.LogError(erreur, "Erreur pendant la connexion de {Identifiant}.", NomUtilisateur);

            MessageErreur = "La connexion a échoué. Vérifiez que le serveur de base de données " +
                            "est accessible, puis réessayez.";
        }
        finally
        {
            EstOccupe = false;
        }
    }

    [RelayCommand]
    private static void Quitter() => System.Windows.Application.Current.Shutdown();
}
