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
        if (EstOccupe)
        {
            return false;
        }

        EstOccupe = true;
        MessageStatut = null;

        try
        {
            await operation().ConfigureAwait(true);

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
        }
    }

    /// <summary>Variante retournant une valeur.</summary>
    protected async Task<T?> ExecuterAsync<T>(
        Func<Task<T>> operation,
        string? messageSucces = null,
        string? contexteJournal = null)
    {
        T? resultat = default;

        await ExecuterAsync(
            async () => resultat = await operation().ConfigureAwait(true),
            messageSucces,
            contexteJournal).ConfigureAwait(true);

        return resultat;
    }
}
