using CommunityToolkit.Mvvm.ComponentModel;
using GestionMagasin.App.Services;
using GestionMagasin.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>
/// Base de toutes les vues-modèles. Elle apporte l'indicateur de chargement,
/// le titre de la page et surtout un traitement uniforme des erreurs :
/// une erreur métier est affichée telle quelle à l'utilisateur, une erreur
/// technique est journalisée puis remplacée par un message compréhensible.
/// </summary>
public abstract partial class VueModeleBase : ObservableObject
{
    protected readonly IServiceDialogue Dialogue;
    protected readonly ILogger Journal;

    /// <summary>
    /// Sérialise les accès aux données de l'écran.
    ///
    /// Un écran déclenche naturellement plusieurs lectures qui se
    /// chevauchent : la liste se recharge, ce qui change la sélection, ce qui
    /// déclenche le chargement de la fiche détaillée. Or le contexte de
    /// données d'Entity Framework n'accepte qu'une opération à la fois. Sans
    /// cette file d'attente, la seconde lecture échoue avec « A second
    /// operation was started on this context instance ».
    /// </summary>
    private readonly SemaphoreSlim _acces = new(1, 1);

    protected VueModeleBase(IServiceDialogue dialogue, ILogger journal)
    {
        Dialogue = dialogue;
        Journal = journal;
    }

    /// <summary>Titre affiché en haut de la page.</summary>
    public abstract string Titre { get; }

    /// <summary>Description courte affichée sous le titre.</summary>
    public virtual string SousTitre => string.Empty;

    [ObservableProperty]
    private bool _estOccupe;

    [ObservableProperty]
    private string? _messageStatut;

    /// <summary>Charge ou recharge les données de la page.</summary>
    public virtual Task ChargerAsync() => Task.CompletedTask;

    /// <summary>
    /// Exécute une opération en affichant l'indicateur d'activité et en
    /// traitant les erreurs de façon homogène.
    /// </summary>
    /// <returns>Vrai si l'opération s'est déroulée sans erreur.</returns>
    protected async Task<bool> ExecuterAsync(
        Func<Task> operation,
        string? messageSucces = null,
        string? contexteJournal = null)
    {
        // Chaque étape est tracée séparément. Un arrêt brutal — pile d'appels
        // saturée — ne laisse ni exception ni trace : seule la dernière ligne
        // écrite avant l'arrêt désigne alors l'instruction fautive.
        var etape = contexteJournal ?? "opération";

        // Les opérations ne sont pas abandonnées mais mises à la suite : une
        // fiche demandée pendant le rechargement d'une liste doit finir par
        // s'afficher, pas disparaître silencieusement.
        Journal.LogDebug("[{Etape}] attente du tour.", etape);

        await _acces.WaitAsync().ConfigureAwait(true);

        Journal.LogDebug("[{Etape}] passage en attente à l'écran.", etape);

        EstOccupe = true;

        Journal.LogDebug("[{Etape}] effacement du message d'état.", etape);

        MessageStatut = null;

        Journal.LogDebug("[{Etape}] appel de l'opération.", etape);

        try
        {
            await operation().ConfigureAwait(true);

            Journal.LogDebug("[{Etape}] opération terminée.", etape);

            if (messageSucces is not null)
            {
                MessageStatut = messageSucces;
                Dialogue.Succes(messageSucces);
            }

            return true;
        }
        catch (ExceptionMetier erreur)
        {
            // Erreur prévue : son message est rédigé pour l'utilisateur.
            Journal.LogWarning(erreur, "Règle métier non respectée : {Message}", erreur.Message);
            MessageStatut = erreur.Message;
            Dialogue.Erreur(erreur.Message, "Opération impossible");

            return false;
        }
        catch (ObjectDisposedException erreur)
        {
            // L'écran a été quitté pendant la lecture : ses services ont été
            // libérés. Il n'y a plus rien à afficher, et rien à signaler.
            Journal.LogDebug(erreur, "Opération abandonnée : l'écran a été fermé.");

            return false;
        }
        catch (Exception erreur)
        {
            // Erreur technique : le détail part dans le journal, l'utilisateur
            // reçoit un message clair et sans jargon.
            Journal.LogError(erreur, "Erreur technique{Contexte}",
                contexteJournal is null ? string.Empty : $" ({contexteJournal})");

            MessageStatut = "Une erreur est survenue.";
            Dialogue.Erreur(
                "Une erreur est survenue lors de l'opération. Veuillez réessayer. " +
                "Si le problème persiste, contactez votre administrateur.",
                "Erreur");

            return false;
        }
        finally
        {
            EstOccupe = false;
            _acces.Release();

            Journal.LogDebug("[{Etape}] écran rendu à l'utilisateur.", etape);
        }
    }

    /// <summary>
    /// Variante retournant une valeur.
    /// </summary>
    /// <remarks>
    /// Le corps de la lambda est un BLOC, et cette forme n'est pas
    /// cosmétique.
    ///
    /// Écrite en expression — « async () => resultat = await operation() » —
    /// l'affectation produit une valeur de type T. La lambda satisfait alors
    /// aussi bien Func&lt;Task&gt; que Func&lt;Task&lt;T&gt;&gt;, et le
    /// compilateur retient la seconde : cette méthode s'appelait elle-même,
    /// indéfiniment, jusqu'à saturer la pile d'appels. Windows arrêtait le
    /// logiciel sur-le-champ, sans exception ni trace, à chaque encaissement.
    ///
    /// Le bloc ne produit aucune valeur : seule Func&lt;Task&gt; convient, et
    /// la surcharge qui travaille est bien celle qui est appelée.
    /// </remarks>
    protected async Task<T?> ExecuterAsync<T>(
        Func<Task<T>> operation,
        string? messageSucces = null,
        string? contexteJournal = null)
    {
        T? resultat = default;

        await ExecuterAsync(
            async () => { resultat = await operation().ConfigureAwait(true); },
            messageSucces,
            contexteJournal).ConfigureAwait(true);

        return resultat;
    }
}
