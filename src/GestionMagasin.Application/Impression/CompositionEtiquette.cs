using GestionMagasin.Application.Common;
using GestionMagasin.Domain.Common;
using ZXing.OneD;

namespace GestionMagasin.Application.Impression;

/// <summary>Article à étiqueter.</summary>
/// <param name="Nom">Nom du produit.</param>
/// <param name="Detail">Taille et couleur.</param>
/// <param name="Prix">Prix de vente.</param>
/// <param name="CodeBarres">Code scanné en caisse.</param>
public sealed record ArticleEtiquette(string Nom, string Detail, decimal Prix, string CodeBarres);

/// <summary>Format du code-barres dessiné sur l'étiquette.</summary>
public enum FormatCodeBarres
{
    /// <summary>EAN-13 : les codes du magasin et ceux des articles du commerce.</summary>
    Ean13,

    /// <summary>Code 128 : tout autre code saisi à la main, lettres comprises.</summary>
    Code128
}

/// <summary>Barre noire, en points de l'imprimante.</summary>
public sealed record BarreEtiquette(int X, int Y, int Largeur, int Hauteur);

/// <summary>
/// Ligne de texte, centrée sur <paramref name="CentreX"/>, dont le haut est
/// à <paramref name="Haut"/>. Le texte trop long pour
/// <paramref name="LargeurMax"/> est raccourci par des points de suspension
/// au dessin.
/// </summary>
/// <param name="Corps">Corps du texte, en points de l'imprimante.</param>
public sealed record TexteEtiquette(string Texte, int CentreX, int Haut, int Corps, int LargeurMax, bool Gras);

/// <summary>Étiquette prête à dessiner : tout y est exprimé en points de l'imprimante.</summary>
public sealed record MiseEnPageEtiquette(
    int Largeur,
    int Hauteur,
    FormatCodeBarres Format,
    int PointsParModule,
    IReadOnlyList<TexteEtiquette> Textes,
    IReadOnlyList<BarreEtiquette> Barres);

/// <summary>Étiquette qu'il est impossible d'imprimer, expliquée en français.</summary>
public sealed class EtiquetteImpossibleException(string message) : Exception(message);

/// <summary>
/// Mise en page d'une étiquette code-barres de vêtement : nom du magasin,
/// article, taille et couleur, prix, puis le code-barres et ses chiffres.
///
/// Tout est calculé en points de l'imprimante, et chaque barre a une largeur
/// d'un nombre entier de points. C'est ce qui rend le code lisible par la
/// douchette : une barre de 2,6 points serait imprimée tantôt sur 2, tantôt
/// sur 3, et le code deviendrait illisible.
/// </summary>
public static class CompositionEtiquette
{
    /// <summary>Résolution de presque toutes les imprimantes d'étiquettes thermiques.</summary>
    public const int ResolutionCourante = 203;

    /// <summary>Hauteur d'une ligne, rapportée au corps du texte.</summary>
    private const double Interligne = 1.2;

    /// <summary>Marge blanche autour de l'étiquette : une étiquette est rarement posée au dixième de millimètre.</summary>
    private const double MargeMm = 1.2;

    /// <summary>
    /// Largeur maximale d'un module (la barre la plus fine). Au-delà, le code
    /// s'élargit sans se lire mieux, et prend la place du texte.
    /// </summary>
    private const double ModuleMaximalMm = 0.42;

    /// <summary>Hauteur des barres visée : une douchette lit sans peine à partir de là.</summary>
    private const double BarresConfortablesMm = 7;

    /// <summary>En dessous, la douchette doit être tenue bien droite : on n'y descend qu'en dernier recours.</summary>
    private const double BarresMinimalesMm = 4.5;

    /// <summary>Au-delà, les barres plus hautes n'apportent rien : l'espace restant est réparti en haut et en bas.</summary>
    private const double BarresMaximalesMm = 15;

    private const double CorpsMinimalMm = 1.4;

    private enum Role
    {
        Magasin,
        Article,
        Detail,
        Prix
    }

    /// <summary>Compose l'étiquette d'un article.</summary>
    /// <param name="article">Article à étiqueter.</param>
    /// <param name="nomMagasin">Nom imprimé en tête, si les réglages le demandent.</param>
    /// <param name="symboleDevise">Symbole de la devise du prix.</param>
    /// <param name="reglages">Dimensions et contenu de l'étiquette.</param>
    /// <param name="pointsParPouce">Résolution de l'imprimante.</param>
    /// <exception cref="EtiquetteImpossibleException">Code illisible ou étiquette trop petite.</exception>
    public static MiseEnPageEtiquette Composer(
        ArticleEtiquette article,
        string? nomMagasin,
        string? symboleDevise,
        ReglagesEtiquettes reglages,
        int pointsParPouce = ResolutionCourante)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(reglages);

        reglages = reglages.Normaliser();

        if (pointsParPouce is < 100 or > 1200)
        {
            pointsParPouce = ResolutionCourante;
        }

        var parMm = pointsParPouce / 25.4;
        int Points(double mm) => (int)Math.Round(mm * parMm);
        static int HauteurLigne(int corps) => (int)Math.Ceiling(corps * Interligne);

        var largeur = Points(reglages.LargeurMm);
        var hauteur = Points(reglages.HauteurMm);
        var marge = Math.Max(2, Points(MargeMm));
        var interieur = largeur - 2 * marge;

        // --- Code-barres : largeur des modules -----------------------------
        var code = article.CodeBarres?.Trim() ?? string.Empty;
        var (format, modules) = Motif(code);

        // Zones blanches de part et d'autre, que la douchette exige. Celle de
        // gauche d'un EAN-13 accueille son premier chiffre.
        var (zoneGauche, zoneDroite) = format == FormatCodeBarres.Ean13 ? (11, 7) : (10, 10);

        var module = Math.Min(largeur / (modules.Length + zoneGauche + zoneDroite), interieur / modules.Length);
        module = Math.Min(module, Math.Max(1, (int)Math.Floor(ModuleMaximalMm * parMm)));

        if (module < 1)
        {
            throw new EtiquetteImpossibleException(
                $"Le code-barres « {code} » est trop long pour une étiquette de {reglages.LargeurMm} mm de " +
                "large. Choisissez une étiquette plus large dans Paramètres, onglet Étiquettes, ou laissez " +
                "le logiciel attribuer à l'article un code du magasin, plus court.");
        }

        // --- Lignes de texte -----------------------------------------------
        var lignes = new List<(Role Role, string Texte, double Mm, bool Gras)>();

        if (reglages.AfficherMagasin && !string.IsNullOrWhiteSpace(nomMagasin))
        {
            lignes.Add((Role.Magasin, nomMagasin.Trim(), 1.9, true));
        }

        lignes.Add((Role.Article, (article.Nom ?? string.Empty).Trim(), 2.5, true));

        if (!string.IsNullOrWhiteSpace(article.Detail))
        {
            lignes.Add((Role.Detail, article.Detail.Trim(), 2.2, false));
        }

        if (reglages.AfficherPrix)
        {
            lignes.Add((Role.Prix, FormaterPrix(article.Prix, symboleDevise), 3.4, true));
        }

        const double chiffresMm = 2.0;
        var espace = Math.Max(1, Points(0.5));

        // Une petite étiquette ne peut pas tout porter en grand : le texte
        // rapetisse un peu, puis le nom du magasin s'efface — l'article, son
        // prix et le code restent toujours.
        (double Echelle, bool AvecMagasin)[] essais =
            [(1.0, true), (0.9, true), (0.9, false), (0.8, false), (0.7, false)];

        foreach (var (echelle, avecMagasin) in essais)
        {
            var retenues = lignes.Where(l => avecMagasin || l.Role != Role.Magasin).ToList();
            var corps = retenues.Select(l => Math.Max(Points(CorpsMinimalMm), Points(l.Mm * echelle))).ToList();
            var corpsChiffres = Math.Max(Points(CorpsMinimalMm), Points(chiffresMm * echelle));

            var barres = hauteur - 2 * marge - corps.Sum(HauteurLigne) - espace - HauteurLigne(corpsChiffres);
            var dernierEssai = (echelle, avecMagasin) == essais[^1];

            if (barres >= Points(BarresConfortablesMm) || (dernierEssai && barres >= Points(BarresMinimalesMm)))
            {
                return Disposer(retenues.Select(l => (l.Texte, l.Gras)).ToList(), corps, corpsChiffres, barres);
            }
        }

        throw new EtiquetteImpossibleException(
            $"Une étiquette de {reglages.LargeurMm} × {reglages.HauteurMm} mm est trop petite pour porter " +
            "l'article et un code-barres lisible. Choisissez une étiquette plus haute dans Paramètres, " +
            "onglet Étiquettes, ou retirez-en le prix ou le nom du magasin.");

        MiseEnPageEtiquette Disposer(List<(string Texte, bool Gras)> retenues, List<int> corps, int corpsChiffres, int barres)
        {
            var hauteurBarres = Math.Min(barres, Points(BarresMaximalesMm));
            var y = marge + (barres - hauteurBarres) / 2;

            var textes = new List<TexteEtiquette>();

            for (var i = 0; i < retenues.Count; i++)
            {
                textes.Add(new TexteEtiquette(retenues[i].Texte, largeur / 2, y, corps[i], interieur, retenues[i].Gras));
                y += HauteurLigne(corps[i]);
            }

            y += espace;

            // Le code est centré avec ses zones blanches, au point près.
            var debut = (largeur - (modules.Length + zoneGauche + zoneDroite) * module) / 2 + zoneGauche * module;
            var hautChiffres = y + hauteurBarres;

            var traits = new List<BarreEtiquette>();
            var m = 0;

            while (m < modules.Length)
            {
                if (!modules[m])
                {
                    m++;
                    continue;
                }

                var depart = m;

                while (m < modules.Length && modules[m])
                {
                    m++;
                }

                // Les barres de garde d'un EAN-13 descendent entre les
                // groupes de chiffres, comme sur tous les articles du commerce.
                var garde = format == FormatCodeBarres.Ean13 && EstGarde(depart);

                traits.Add(new BarreEtiquette(
                    debut + depart * module,
                    y,
                    (m - depart) * module,
                    hauteurBarres + (garde ? HauteurLigne(corpsChiffres) / 2 : 0)));
            }

            if (format == FormatCodeBarres.Ean13)
            {
                textes.Add(new TexteEtiquette(code[..1], debut - 6 * module, hautChiffres, corpsChiffres, 9 * module, false));
                textes.Add(new TexteEtiquette(code[1..7], debut + 24 * module, hautChiffres, corpsChiffres, 42 * module, false));
                textes.Add(new TexteEtiquette(code[7..], debut + 71 * module, hautChiffres, corpsChiffres, 42 * module, false));
            }
            else
            {
                textes.Add(new TexteEtiquette(code, largeur / 2, hautChiffres, corpsChiffres, interieur, false));
            }

            return new MiseEnPageEtiquette(largeur, hauteur, format, module, textes, traits);
        }
    }

    /// <summary>
    /// Prix tel qu'on l'affiche sur un vêtement : « 2 500 DA », sans les
    /// centimes quand il n'y en a pas.
    /// </summary>
    public static string FormaterPrix(decimal prix, string? symboleDevise)
    {
        var format = prix == decimal.Truncate(prix) ? "#,0" : "#,0.00";

        return $"{prix.ToString(format, FormatageMontant.CultureApplication)} " +
               $"{(string.IsNullOrWhiteSpace(symboleDevise) ? FormatageMontant.SymboleParDefaut : symboleDevise)}";
    }

    /// <summary>Taille et couleur, dans l'ordre où on les cherche sur une étiquette.</summary>
    public static string Detail(string? taille, string? couleur) =>
        string.Join(" — ", new[] { string.IsNullOrWhiteSpace(taille) ? null : $"Taille {taille.Trim()}", couleur?.Trim() }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>Barres de garde d'un EAN-13 : début, milieu et fin.</summary>
    private static bool EstGarde(int module) => module is 0 or 2 or 46 or 48 or 92 or 94;

    private static (FormatCodeBarres Format, bool[] Modules) Motif(string code)
    {
        if (code.Length == 0)
        {
            throw new EtiquetteImpossibleException(
                "Cet article n'a pas de code-barres. Ouvrez la déclinaison et enregistrez-la : le " +
                "logiciel lui en attribue un.");
        }

        if (CodesBarres.EstEan13(code))
        {
            return (FormatCodeBarres.Ean13, new EAN13Writer().encode(code));
        }

        try
        {
            return (FormatCodeBarres.Code128, new Code128Writer().encode(code));
        }
        catch (ArgumentException)
        {
            throw new EtiquetteImpossibleException(
                $"Le code-barres « {code} » contient des caractères qu'un code-barres ne peut pas porter " +
                "(accents, symboles). Corrigez-le dans la déclinaison, ou videz-le pour que le logiciel en " +
                "attribue un.");
        }
    }
}
