using System.Windows;
using GestionMagasin.App.Views.Dialogues;
using Microsoft.Win32;

namespace GestionMagasin.App.Services;

/// <inheritdoc cref="IServiceDialogue"/>
public class ServiceDialogue : IServiceDialogue
{
    public void Informer(string message, string titre = "Information") =>
        Afficher(message, titre, MessageBoxImage.Information);

    public void Succes(string message) =>
        Afficher(message, "Opération réussie", MessageBoxImage.Information);

    public void Avertir(string message, string titre = "Attention") =>
        Afficher(message, titre, MessageBoxImage.Warning);

    public void Erreur(string message, string titre = "Erreur") =>
        Afficher(message, titre, MessageBoxImage.Error);

    public bool Confirmer(string message, string titre = "Confirmation")
    {
        var resultat = Executer(() => MessageBox.Show(
            FenetreActive(),
            message,
            titre,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No));

        return resultat == MessageBoxResult.Yes;
    }

    public string? DemanderTexte(string message, string titre, string valeurInitiale = "") =>
        Executer(() =>
        {
            var fenetre = new FenetreSaisieTexte(titre, message, valeurInitiale)
            {
                Owner = FenetreActive()
            };

            return fenetre.ShowDialog() == true ? fenetre.Valeur : null;
        });

    public string? DemanderCheminEnregistrement(string nomParDefaut, string filtre) =>
        Executer(() =>
        {
            var boite = new SaveFileDialog
            {
                FileName = nomParDefaut,
                Filter = filtre,
                Title = "Enregistrer le document",
                AddExtension = true,
                OverwritePrompt = true
            };

            return boite.ShowDialog() == true ? boite.FileName : null;
        });

    public string? DemanderCheminOuverture(string filtre) =>
        Executer(() =>
        {
            var boite = new OpenFileDialog
            {
                Filter = filtre,
                Title = "Sélectionner un fichier",
                CheckFileExists = true
            };

            return boite.ShowDialog() == true ? boite.FileName : null;
        });

    private static void Afficher(string message, string titre, MessageBoxImage icone) =>
        Executer(() => MessageBox.Show(FenetreActive(), message, titre, MessageBoxButton.OK, icone));

    /// <summary>
    /// Exécute l'affichage sur le fil d'exécution de l'interface. Les
    /// vues-modèles travaillant en arrière-plan, un message peut être demandé
    /// depuis un autre fil.
    /// </summary>
    private static T Executer<T>(Func<T> action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.Invoke(action);
    }

    private static Window? FenetreActive() =>
        System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(f => f.IsActive)
        ?? System.Windows.Application.Current?.MainWindow;
}
