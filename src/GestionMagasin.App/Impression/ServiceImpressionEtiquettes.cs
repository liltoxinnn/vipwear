using System.ComponentModel;
using System.IO;
using System.Printing;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GestionMagasin.Application.Impression;
using GestionMagasin.Application.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.Impression;

/// <summary>Échec d'impression des étiquettes, décrit en français pour le magasin.</summary>
public sealed class ImpressionEtiquettesException(string message, Exception? cause = null)
    : Exception(message, cause);

/// <summary>Étiquettes à imprimer pour un article.</summary>
public sealed record DemandeEtiquettes(ArticleEtiquette Article, int Nombre);

/// <summary>Impression des étiquettes code-barres sur l'imprimante d'étiquettes du poste.</summary>
public interface IServiceImpressionEtiquettes
{
    /// <summary>Réglages actuels du poste.</summary>
    ReglagesEtiquettes Reglages { get; }

    /// <summary>Enregistre les réglages du poste.</summary>
    void Enregistrer(ReglagesEtiquettes reglages);

    /// <summary>Imprimantes installées dans Windows, sans les imprimantes virtuelles.</summary>
    IReadOnlyList<string> ListerImprimantes();

    /// <summary>Image de l'étiquette d'un article, telle qu'elle sortira.</summary>
    /// <exception cref="EtiquetteImpossibleException">Code illisible ou étiquette trop petite.</exception>
    Task<BitmapSource> ApercuAsync(ArticleEtiquette article, ReglagesEtiquettes reglages);

    /// <summary>Imprime les étiquettes demandées et retourne leur nombre.</summary>
    /// <exception cref="ImpressionEtiquettesException">Imprimante absente, refus de Windows.</exception>
    /// <exception cref="EtiquetteImpossibleException">Un article ne tient pas sur l'étiquette.</exception>
    Task<int> ImprimerAsync(IReadOnlyList<DemandeEtiquettes> demandes, ReglagesEtiquettes reglages);
}

/// <summary>
/// Chaîne complète d'une impression d'étiquettes : composition, dessin à la
/// résolution de l'imprimante, une page par étiquette, envoi au pilote.
///
/// Contrairement aux tickets, les étiquettes passent par le pilote de
/// l'imprimante : chaque fabricant (Xprinter, TSC, Zebra, Godex…) parle sa
/// propre langue, et seul son pilote la connaît. La page a exactement la
/// taille de l'étiquette ; le pilote sait alors, grâce au repère entre deux
/// étiquettes, où commence la suivante.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ServiceImpressionEtiquettes : IServiceImpressionEtiquettes
{
    /// <summary>Au-delà, c'est une faute de frappe : 5000 étiquettes au lieu de 50.</summary>
    public const int NombreMaximal = 2000;

    /// <summary>Fichier des réglages du poste, à côté de celui des tickets.</summary>
    public static string CheminReglages { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GestionMagasin",
        "imprimante-etiquettes.json");

    private readonly IServiceParametres _parametres;
    private readonly ILogger<ServiceImpressionEtiquettes> _journal;

    /// <summary>
    /// Fiche du magasin, lue une seule fois. Ouvrir la fenêtre déclenche
    /// plusieurs aperçus à la fois : sans ce partage, chacun interrogerait la
    /// base en même temps, ce que la connexion de la fenêtre refuse.
    /// </summary>
    private Task<Domain.Entities.ParametresMagasin>? _magasin;

    public ServiceImpressionEtiquettes(IServiceParametres parametres, ILogger<ServiceImpressionEtiquettes> journal)
    {
        _parametres = parametres;
        _journal = journal;
    }

    public ReglagesEtiquettes Reglages => Lire();

    /// <summary>Fiche du magasin ; relue seulement si la lecture précédente a échoué.</summary>
    private Task<Domain.Entities.ParametresMagasin> Magasin()
    {
        if (_magasin is null || _magasin.IsFaulted || _magasin.IsCanceled)
        {
            _magasin = _parametres.ObtenirAsync();
        }

        return _magasin;
    }

    public void Enregistrer(ReglagesEtiquettes reglages)
    {
        var normalises = reglages.Normaliser();

        Directory.CreateDirectory(Path.GetDirectoryName(CheminReglages)!);

        // Écriture dans un fichier voisin puis remplacement : une coupure de
        // courant laisse l'ancien fichier intact.
        var provisoire = CheminReglages + ".tmp";
        File.WriteAllText(provisoire, normalises.VersJson());
        File.Move(provisoire, CheminReglages, overwrite: true);

        _journal.LogInformation(
            "Imprimante d'étiquettes : « {Imprimante} », étiquettes de {Largeur} × {Hauteur} mm.",
            normalises.NomImprimante ?? "aucune", normalises.LargeurMm, normalises.HauteurMm);
    }

    public IReadOnlyList<string> ListerImprimantes() => SpouleurWindows.ListerImprimantes();

    public async Task<BitmapSource> ApercuAsync(ArticleEtiquette article, ReglagesEtiquettes reglages)
    {
        var magasin = await Magasin().ConfigureAwait(true);

        var etiquette = CompositionEtiquette.Composer(
            article, magasin.NomMagasin, magasin.SymboleDevise, reglages, CompositionEtiquette.ResolutionCourante);

        return RenduEtiquette.Dessiner(etiquette, CompositionEtiquette.ResolutionCourante);
    }

    public async Task<int> ImprimerAsync(IReadOnlyList<DemandeEtiquettes> demandes, ReglagesEtiquettes reglages)
    {
        ArgumentNullException.ThrowIfNull(demandes);

        reglages = reglages.Normaliser();

        var aImprimer = demandes.Where(d => d.Nombre > 0).ToList();
        var total = aImprimer.Sum(d => d.Nombre);

        if (total == 0)
        {
            return 0;
        }

        if (total > NombreMaximal)
        {
            throw new ImpressionEtiquettesException(
                $"{total} étiquettes d'un coup, c'est sans doute une faute de frappe. Le maximum est de " +
                $"{NombreMaximal} par impression : vérifiez les nombres saisis.");
        }

        if (!reglages.EstConfiguree)
        {
            throw new ImpressionEtiquettesException("Choisissez d'abord l'imprimante d'étiquettes.");
        }

        var nomImprimante = reglages.NomImprimante!;

        if (SpouleurWindows.EstVirtuelle(nomImprimante))
        {
            throw new ImpressionEtiquettesException(
                $"« {nomImprimante} » n'est pas une imprimante : elle fabrique un fichier. Choisissez " +
                "l'imprimante d'étiquettes dans la liste.");
        }

        var magasin = await Magasin().ConfigureAwait(true);

        using var serveur = new LocalPrintServer();
        using var file = Trouver(serveur, nomImprimante)
            ?? throw new ImpressionEtiquettesException(
                $"L'imprimante « {nomImprimante} » est introuvable. Elle a peut-être été renommée ou retirée " +
                "de Windows : choisissez-la de nouveau dans la liste.");

        try
        {
            var (ticket, resolution) = PreparerTicket(file, reglages);

            // Toutes les étiquettes sont composées avant le premier envoi : un
            // article qui ne tient pas sur l'étiquette arrête tout, au lieu de
            // laisser une série à moitié imprimée.
            var images = aImprimer
                .Select(d => (Image: Dessiner(d.Article, magasin.NomMagasin, magasin.SymboleDevise, reglages, resolution),
                              d.Nombre))
                .ToList();

            var largeurPage = reglages.LargeurMm / 25.4 * 96;
            var hauteurPage = reglages.HauteurMm / 25.4 * 96;

            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = new Size(largeurPage, hauteurPage);

            foreach (var (image, nombre) in images)
            {
                for (var i = 0; i < nombre; i++)
                {
                    document.Pages.Add(Page(image, largeurPage, hauteurPage));
                }
            }

            _journal.LogInformation(
                "Étiquettes : {Total} vers « {Imprimante} », {Largeur} × {Hauteur} mm à {Resolution} points par pouce.",
                total, nomImprimante, reglages.LargeurMm, reglages.HauteurMm, resolution);

            PrintQueue.CreateXpsDocumentWriter(file).Write(document, ticket);

            return total;
        }
        catch (Exception erreur) when (erreur is PrintSystemException or Win32Exception
                                           or InvalidOperationException or IOException)
        {
            _journal.LogError(erreur, "Étiquettes : envoi à « {Imprimante} » impossible.", nomImprimante);

            throw new ImpressionEtiquettesException(
                $"Windows n'a pas pu envoyer les étiquettes à « {nomImprimante} ». Vérifiez qu'elle est " +
                "allumée, branchée et approvisionnée, puis réessayez." +
                Environment.NewLine + Environment.NewLine + "Détail : " + erreur.Message,
                erreur);
        }
    }

    private static BitmapSource Dessiner(
        ArticleEtiquette article,
        string nomMagasin,
        string symboleDevise,
        ReglagesEtiquettes reglages,
        int resolution)
    {
        try
        {
            return RenduEtiquette.Dessiner(
                CompositionEtiquette.Composer(article, nomMagasin, symboleDevise, reglages, resolution),
                resolution);
        }
        catch (EtiquetteImpossibleException erreur)
        {
            throw new EtiquetteImpossibleException($"« {article.Nom} — {article.Detail} » : {erreur.Message}");
        }
    }

    /// <summary>
    /// Réglage du travail : une page de la taille de l'étiquette, et la
    /// résolution de l'imprimante, lue dans son pilote.
    /// </summary>
    private static (PrintTicket Ticket, int Resolution) PreparerTicket(PrintQueue file, ReglagesEtiquettes reglages)
    {
        var depart = file.UserPrintTicket ?? file.DefaultPrintTicket;
        var demande = depart.Clone();

        demande.PageMediaSize = new PageMediaSize(reglages.LargeurMm / 25.4 * 96, reglages.HauteurMm / 25.4 * 96);
        demande.PageOrientation = PageOrientation.Portrait;
        demande.CopyCount = 1;

        var ticket = file.MergeAndValidatePrintTicket(depart, demande).ValidatedPrintTicket;

        var resolution = ticket.PageResolution?.X ?? CompositionEtiquette.ResolutionCourante;

        return (ticket, resolution is >= 150 and <= 1200 ? resolution : CompositionEtiquette.ResolutionCourante);
    }

    private static PageContent Page(BitmapSource image, double largeur, double hauteur)
    {
        var dessin = new Image { Source = image, Width = largeur, Height = hauteur, Stretch = Stretch.Fill };

        // L'image a déjà la résolution de l'imprimante : rien à lisser.
        RenderOptions.SetBitmapScalingMode(dessin, BitmapScalingMode.NearestNeighbor);

        var page = new FixedPage { Width = largeur, Height = hauteur, Background = Brushes.White };
        page.Children.Add(dessin);

        var contenu = new PageContent();
        ((System.Windows.Markup.IAddChild)contenu).AddChild(page);

        return contenu;
    }

    /// <summary>File d'impression du nom donné, locale ou partagée par un autre poste.</summary>
    private static PrintQueue? Trouver(LocalPrintServer serveur, string nom)
    {
        PrintQueue? trouvee = null;

        foreach (var file in serveur.GetPrintQueues(
                     [EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]))
        {
            if (trouvee is null && string.Equals(file.FullName, nom, StringComparison.OrdinalIgnoreCase))
            {
                trouvee = file;
            }
            else
            {
                file.Dispose();
            }
        }

        return trouvee;
    }

    private static ReglagesEtiquettes Lire()
    {
        try
        {
            return File.Exists(CheminReglages)
                ? ReglagesEtiquettes.DepuisJson(File.ReadAllText(CheminReglages))
                : new ReglagesEtiquettes();
        }
        catch (Exception erreur) when (erreur is IOException or UnauthorizedAccessException)
        {
            return new ReglagesEtiquettes();
        }
    }
}
