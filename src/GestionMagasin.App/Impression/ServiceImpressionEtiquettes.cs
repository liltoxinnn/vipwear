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

    /// <summary>
    /// Vérifie que le pilote de l'imprimante imprimera bien sur la taille
    /// des étiquettes.
    /// </summary>
    /// <returns>
    /// L'explication du désaccord et la marche à suivre, ou rien quand la
    /// taille convient — ou ne peut pas être vérifiée.
    /// </returns>
    string? VerifierFormat(ReglagesEtiquettes reglages);

    /// <summary>
    /// Imprime une série d'étiquettes de réglage, encadrées : sur une
    /// imprimante de tickets, elles montrent de combien déplacer l'impression.
    /// </summary>
    Task ImprimerReglageAsync(ReglagesEtiquettes reglages);

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

        if (reglages.SurImprimanteDeTickets)
        {
            var images = aImprimer
                .Select(d => Image(d.Article, magasin.NomMagasin, magasin.SymboleDevise, reglages, d.Nombre))
                .ToList();

            await EnvoyerSurTicketsAsync(images, reglages, "Étiquettes").ConfigureAwait(true);

            return total;
        }

        using var serveur = new LocalPrintServer();
        using var file = Trouver(serveur, nomImprimante)
            ?? throw new ImpressionEtiquettesException(
                $"L'imprimante « {nomImprimante} » est introuvable. Elle a peut-être été renommée ou retirée " +
                "de Windows : choisissez-la de nouveau dans la liste.");

        try
        {
            var (ticket, resolution, _) = PreparerTicket(file, reglages);

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

    public async Task ImprimerReglageAsync(ReglagesEtiquettes reglages)
    {
        reglages = reglages.Normaliser();

        if (!reglages.EstConfiguree)
        {
            throw new ImpressionEtiquettesException("Choisissez d'abord l'imprimante d'étiquettes.");
        }

        if (!reglages.SurImprimanteDeTickets)
        {
            throw new ImpressionEtiquettesException(
                "Le réglage ne concerne que les imprimantes de tickets : une imprimante d'étiquettes repère " +
                "elle-même chaque étiquette.");
        }

        // Cinq étiquettes : un glissement d'une étiquette à l'autre — espace
        // mal réglé — se voit nettement sur la cinquième.
        var images = Enumerable.Range(1, NombreEtiquettesDeReglage)
            .Select(numero =>
            {
                var mise = CompositionEtiquette.Reglage(reglages, numero, BandeEtiquettes.Resolution);
                return new ImageEtiquette(RenduEtiquette.Gris(mise), mise.Largeur, mise.Hauteur, 1);
            })
            .ToList();

        await EnvoyerSurTicketsAsync(images, reglages, "Réglage des étiquettes").ConfigureAwait(true);
    }

    /// <summary>Nombre d'étiquettes d'une série de réglage.</summary>
    public const int NombreEtiquettesDeReglage = 5;

    /// <summary>
    /// Envoi direct d'une série à une imprimante de tickets, comme les tickets
    /// de caisse : une seule image, au pas exact des étiquettes.
    /// </summary>
    private async Task EnvoyerSurTicketsAsync(
        IReadOnlyList<ImageEtiquette> images,
        ReglagesEtiquettes reglages,
        string nomDocument)
    {
        var nomImprimante = reglages.NomImprimante!;

        if (SpouleurWindows.EstVirtuelle(nomImprimante))
        {
            throw new ImpressionEtiquettesException(
                $"« {nomImprimante} » n'est pas une imprimante : elle fabrique un fichier. Choisissez " +
                "l'imprimante dans la liste.");
        }

        var (gris, largeur, hauteur) = BandeEtiquettes.Assembler(images, reglages);

        _journal.LogInformation(
            "{Document} : {Nombre} étiquette(s) de {Largeur} × {Hauteur} mm, pas de {Pas} points, vers la " +
            "tête de {Tete} points de « {Imprimante} ».",
            nomDocument, images.Sum(i => i.Nombre), reglages.LargeurMm, reglages.HauteurMm,
            BandeEtiquettes.Pas(reglages), largeur, nomImprimante);

        try
        {
            await Task.Run(() =>
            {
                var points = Monochrome.VersPoints(gris, largeur, hauteur);
                SpouleurWindows.EnvoyerBrut(nomImprimante, nomDocument, EscPos.Etiquettes(points, largeur, hauteur));
            }).ConfigureAwait(true);
        }
        catch (ImpressionTicketException erreur)
        {
            throw new ImpressionEtiquettesException(erreur.Message, erreur);
        }
    }

    /// <summary>Étiquette d'un article, dessinée pour une imprimante de tickets.</summary>
    private static ImageEtiquette Image(
        ArticleEtiquette article,
        string nomMagasin,
        string symboleDevise,
        ReglagesEtiquettes reglages,
        int nombre)
    {
        try
        {
            var mise = CompositionEtiquette.Composer(article, nomMagasin, symboleDevise, reglages, BandeEtiquettes.Resolution);

            return new ImageEtiquette(RenduEtiquette.Gris(mise), mise.Largeur, mise.Hauteur, nombre);
        }
        catch (EtiquetteImpossibleException erreur)
        {
            throw new EtiquetteImpossibleException($"« {article.Nom} » : {erreur.Message}");
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

    public string? VerifierFormat(ReglagesEtiquettes reglages)
    {
        reglages = reglages.Normaliser();

        // Une imprimante de tickets reçoit les étiquettes directement : son
        // pilote, et son papier, n'entrent pas en jeu.
        if (!reglages.EstConfiguree || PropositionImprimante.EstVirtuelle(reglages.NomImprimante)
                                    || reglages.SurImprimanteDeTickets)
        {
            return null;
        }

        try
        {
            using var serveur = new LocalPrintServer();
            using var file = Trouver(serveur, reglages.NomImprimante!);

            // Une imprimante introuvable est signalée à l'impression, avec
            // son propre message.
            return file is null ? null : PreparerTicket(file, reglages).Probleme;
        }
        catch (Exception erreur) when (erreur is PrintSystemException or Win32Exception
                                           or InvalidOperationException)
        {
            _journal.LogDebug(erreur, "Format du pilote des étiquettes non vérifiable.");
            return null;
        }
    }

    /// <summary>
    /// Réglage du travail : une page de la taille de l'étiquette, et la
    /// résolution de l'imprimante, lue dans son pilote.
    ///
    /// Le pilote a le dernier mot sur la taille du papier. Beaucoup ignorent
    /// une taille qu'ils ne connaissent pas et gardent la leur — 60 × 40 ou
    /// 4 × 6 pouces, par exemple. L'imprimante, qui centre le rouleau sous sa
    /// tête, pose alors l'image de travers : décalée sur le côté, et à
    /// cheval sur deux étiquettes. On demande donc en priorité un format du
    /// pilote lui-même aux bonnes dimensions, puis on vérifie ce qu'il a
    /// réellement retenu.
    /// </summary>
    private static (PrintTicket Ticket, int Resolution, string? Probleme) PreparerTicket(
        PrintQueue file,
        ReglagesEtiquettes reglages)
    {
        var largeur = reglages.LargeurMm / 25.4 * 96;
        var hauteur = reglages.HauteurMm / 25.4 * 96;

        var depart = file.UserPrintTicket ?? file.DefaultPrintTicket;
        var demande = depart.Clone();

        demande.PageMediaSize = FormatDuPilote(file, depart, largeur, hauteur) ?? new PageMediaSize(largeur, hauteur);
        demande.PageOrientation = PageOrientation.Portrait;
        demande.CopyCount = 1;

        var ticket = file.MergeAndValidatePrintTicket(depart, demande).ValidatedPrintTicket;

        var resolution = ticket.PageResolution?.X ?? CompositionEtiquette.ResolutionCourante;

        if (resolution is < 150 or > 1200)
        {
            resolution = CompositionEtiquette.ResolutionCourante;
        }

        // Un pilote qui ne dit rien de son papier n'est pas mis en cause :
        // on ne sait pas, et l'on ne bloque pas une impression peut-être bonne.
        var retenu = ticket.PageMediaSize;
        var probleme = retenu?.Width is { } l && retenu.Height is { } h
                       && !FormatPilote.Convient(EnMm(l), EnMm(h), reglages)
            ? FormatPilote.Expliquer(file.FullName, reglages, EnMm(l), EnMm(h))
            : null;

        return (ticket, resolution, probleme);
    }

    /// <summary>Format déclaré par le pilote aux dimensions de l'étiquette, s'il en a un.</summary>
    private static PageMediaSize? FormatDuPilote(PrintQueue file, PrintTicket depart, double largeur, double hauteur)
    {
        try
        {
            return file.GetPrintCapabilities(depart).PageMediaSizeCapability?
                .FirstOrDefault(f => f.Width is { } l && f.Height is { } h
                                     && Math.Abs(EnMm(l) - EnMm(largeur)) <= FormatPilote.ToleranceMm
                                     && Math.Abs(EnMm(h) - EnMm(hauteur)) <= FormatPilote.ToleranceMm);
        }
        catch (Exception erreur) when (erreur is PrintSystemException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Unités de WPF (1/96 de pouce) en millimètres.</summary>
    private static double EnMm(double unites) => unites * 25.4 / 96;

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
