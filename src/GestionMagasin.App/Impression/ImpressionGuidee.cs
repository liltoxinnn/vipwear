using System.IO;
using GestionMagasin.App.Services;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.Impression;

/// <summary>
/// Ce qui précède l'impression d'un ticket, depuis la caisse comme depuis
/// l'écran Ventes.
///
/// Un poste qui n'a pas encore d'imprimante de tickets ne renvoie plus vers
/// l'écran Paramètres, et ne retombe plus sans prévenir sur le PDF : il pose
/// la question une fois, là où l'on veut imprimer, retient la réponse, et
/// imprime. C'était le piège de la première version — on appuyait sur
/// « Imprimer le ticket », et c'est la fenêtre d'enregistrement d'un PDF qui
/// s'ouvrait.
/// </summary>
internal static class ImpressionGuidee
{
    /// <summary>
    /// S'assure qu'une imprimante de tickets est retenue sur ce poste, en la
    /// demandant s'il le faut.
    /// </summary>
    /// <returns>
    /// <see cref="SuiteChoixImprimante.Imprimer"/> quand le ticket peut
    /// partir sur l'imprimante ; <see cref="SuiteChoixImprimante.Pdf"/> pour
    /// le produire en PDF ; <see cref="SuiteChoixImprimante.Annuler"/> pour
    /// ne rien imprimer.
    /// </returns>
    public static SuiteChoixImprimante Preparer(
        IServiceImpressionTicket impression,
        IServiceDialogue dialogue,
        ILogger journal)
    {
        var reglages = impression.Reglages;
        string? avertissement = null;

        if (reglages.EstConfiguree)
        {
            // Une imprimante PDF retenue par erreur ne produit que des
            // fichiers vides : la question est reposée, en disant pourquoi.
            if (!impression.EstVirtuelle(reglages.NomImprimante!))
            {
                return SuiteChoixImprimante.Imprimer;
            }

            journal.LogWarning(
                "Imprimante de tickets : « {Imprimante} » est une imprimante virtuelle, la question est reposée.",
                reglages.NomImprimante);

            avertissement =
                $"« {reglages.NomImprimante} » n'est pas une imprimante : elle fabrique un fichier PDF " +
                "qui sortirait vide. Choisissez l'imprimante de tickets, ou « En PDF, cette fois ».";
        }

        var choix = dialogue.ChoisirImprimanteTicket(impression.ListerImprimantes(), avertissement);

        if (choix.Suite != SuiteChoixImprimante.Imprimer)
        {
            return choix.Suite;
        }

        try
        {
            impression.Enregistrer(reglages with { NomImprimante = choix.Nom, Largeur = choix.Largeur });

            return SuiteChoixImprimante.Imprimer;
        }
        catch (Exception erreur) when (erreur is IOException or UnauthorizedAccessException)
        {
            journal.LogError(erreur, "Imprimante de tickets : le choix n'a pas pu être retenu.");

            dialogue.Avertir(
                "L'imprimante choisie n'a pas pu être retenue sur ce poste : " + erreur.Message +
                Environment.NewLine + Environment.NewLine +
                "Choisissez-la dans Paramètres, onglet Imprimante de tickets.",
                "Imprimante non retenue");

            return SuiteChoixImprimante.Annuler;
        }
    }
}
