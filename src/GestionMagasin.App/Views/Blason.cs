using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace GestionMagasin.App.Views;

/// <summary>
/// Emblème dessiné du magasin : le lion couronné, l'anneau noir et or, le nom
/// de l'enseigne courbé sur le pourtour.
///
/// Tout est vectoriel et produit une seule fois au premier affichage. Aucun
/// fichier image n'est nécessaire : le blason reste net du bandeau de 36
/// pixels du menu jusqu'au grand format de l'écran de connexion, et il ne
/// peut pas être perdu au moment de livrer le logiciel.
///
/// Deux repères coexistent :
/// — le repère de l'emblème, un carré de 200 sur 200 centré en (100, 100),
///   qui porte les anneaux, le texte courbé et les étoiles ;
/// — le repère du lion, un rectangle de 114 sur 136 déjà mis à l'échelle,
///   que les gabarits posent où ils veulent. Le menu n'affiche que le lion,
///   l'écran de connexion affiche l'emblème entier.
/// </summary>
internal static class Blason
{
    // ------------------------------------------------------------------
    // Repères
    // ------------------------------------------------------------------

    /// <summary>Côté du carré dans lequel l'emblème complet est dessiné.</summary>
    public const double Cote = 200d;

    /// <summary>Largeur du rectangle dans lequel le lion couronné est dessiné.</summary>
    public const double LargeurLion = 104d;

    /// <summary>Hauteur du rectangle dans lequel le lion couronné est dessiné.</summary>
    public const double HauteurLion = 112d;

    /// <summary>Position du lion dans le repère de l'emblème.</summary>
    public const double LionGauche = 48d;

    /// <summary>Position du lion dans le repère de l'emblème.</summary>
    public const double LionHaut = 44d;

    // Le lion est composé dans un carré de 100 sur 100, plus lisible à
    // écrire, puis reporté dans son rectangle définitif.
    private const double EchelleLion = 1.00d;
    private const double DecalageLionX = 2.0d;
    private const double DecalageLionY = 6.0d;

    private const double EchelleCouronne = 0.48d;
    private const double DecalageCouronneX = 28.0d;
    private const double DecalageCouronneY = 3.4d;

    // Centre et rayons de la crinière, exprimés dans le repère du lion.
    private const double CentreX = 52d;
    private const double CentreY = 60d;

    // ------------------------------------------------------------------
    // Tracés du visage, écrits dans le carré de 100 sur 100
    // ------------------------------------------------------------------

    private const string TraceOreilles =
        "F1 M 30,35 C 24,28 26,19 34,22 C 38,23 39,30 38,35 Z " +
        "M 70,35 C 76,28 74,19 66,22 C 62,23 61,30 62,35 Z";

    private const string TraceVisage =
        "F1 M 50,27 C 63,27 73,37 73,50 C 73,57 71,63 68,69 " +
        "C 65,75 58,82 50,82 C 42,82 35,75 32,69 " +
        "C 29,63 27,57 27,50 C 27,37 37,27 50,27 Z";

    // Arcades, yeux, truffe et gueule : des creux noirs posés sur le visage.
    private const string TraceTraits =
        "F1 M 28,43 L 47,50 L 46,54 L 29,48 Z " +
        "M 72,43 L 53,50 L 54,54 L 71,48 Z " +
        "M 33,55 C 35,52 42,52 45,55 C 42,58 35,58 33,55 Z " +
        "M 67,55 C 65,52 58,52 55,55 C 58,58 65,58 67,55 Z " +
        "M 50,59 C 54,59 57,61 56,64 C 55,66 52,68 50,68 " +
        "C 48,68 45,66 44,64 C 43,61 46,59 50,59 Z " +
        "M 50,68 C 48,74 42,75 40,70 C 41,78 49,78 50,71 " +
        "C 51,78 59,78 60,70 C 58,75 52,74 50,68 Z";

    // Trois pointes surmontées de leurs perles, et le bandeau.
    private const string TraceCouronne =
        "F1 M 13,41 L 20,6 L 35,26 L 50,1 L 65,26 L 80,6 L 87,41 Z " +
        "M 10,42 H 90 V 55 H 10 Z " +
        "M 14,4 A 6,6 0 1 0 26,4 A 6,6 0 1 0 14,4 Z " +
        "M 43.5,0 A 6.5,6.5 0 1 0 56.5,0 A 6.5,6.5 0 1 0 43.5,0 Z " +
        "M 74,4 A 6,6 0 1 0 86,4 A 6,6 0 1 0 74,4 Z";

    // ------------------------------------------------------------------
    // Géométries publiques
    // ------------------------------------------------------------------

    private static readonly Lazy<Geometry> CriniereLongueChargee = new(() =>
        Figer(Criniere(17, 24d * EchelleLion, 47d * EchelleLion, 18d, 0d, 0.10d)));

    private static readonly Lazy<Geometry> CriniereCourteChargee = new(() =>
        Figer(Criniere(17, 20d * EchelleLion, 34d * EchelleLion, 14d, 180d / 17d, 0.06d)));

    private static readonly Lazy<Geometry> DisqueCharge = new(() =>
        Figer(new EllipseGeometry(new Point(CentreX, CentreY), 26d * EchelleLion, 26d * EchelleLion)));

    private static readonly Lazy<Geometry> OreillesChargees = new(() => Figer(Reporter(TraceOreilles)));
    private static readonly Lazy<Geometry> VisageCharge = new(() => Figer(Reporter(TraceVisage)));
    private static readonly Lazy<Geometry> TraitsCharges = new(() => Figer(Reporter(TraceTraits)));

    private static readonly Lazy<Geometry> CouronneChargee = new(() =>
        Figer(Poser(
            Geometry.Parse(TraceCouronne),
            EchelleCouronne,
            DecalageCouronneX,
            DecalageCouronneY)));

    private static readonly Lazy<Geometry> TexteHautCharge = new(() =>
        Figer(TexteEnArc("VIP MEN’S STORE", 70d, -90d, 18d, false, 1.6d)));

    private static readonly Lazy<Geometry> TexteBasCharge = new(() =>
        Figer(TexteEnArc("BEJAIA", 87d, 90d, 17d, true, 7.5d)));

    private static readonly Lazy<Geometry> EtoilesChargees = new(() =>
    {
        var groupe = new GeometryGroup { FillRule = FillRule.Nonzero };

        groupe.Children.Add(Geometry.Parse(Etoile(21.5d, 100d, 8d)));
        groupe.Children.Add(Geometry.Parse(Etoile(178.5d, 100d, 8d)));

        return Figer(groupe);
    });

    /// <summary>Premier rang de mèches, le plus long : il donne la silhouette.</summary>
    public static Geometry CriniereLongue => CriniereLongueChargee.Value;

    /// <summary>Second rang de mèches, décalé d'une demi-mèche : il donne l'épaisseur.</summary>
    public static Geometry CriniereCourte => CriniereCourteChargee.Value;

    /// <summary>Disque plein sur lequel repose le visage.</summary>
    public static Geometry Disque => DisqueCharge.Value;

    /// <summary>Oreilles, dessinées sous le visage pour n'en laisser dépasser que la pointe.</summary>
    public static Geometry Oreilles => OreillesChargees.Value;

    /// <summary>Masque du visage.</summary>
    public static Geometry Visage => VisageCharge.Value;

    /// <summary>Arcades, yeux, truffe et gueule.</summary>
    public static Geometry Traits => TraitsCharges.Value;

    /// <summary>Couronne posée sur la crinière.</summary>
    public static Geometry Couronne => CouronneChargee.Value;

    /// <summary>« VIP MEN'S STORE », courbé sur le haut de l'anneau.</summary>
    public static Geometry TexteHaut => TexteHautCharge.Value;

    /// <summary>« BEJAIA », courbé sur le bas de l'anneau.</summary>
    public static Geometry TexteBas => TexteBasCharge.Value;

    /// <summary>Les deux étoiles qui séparent le nom de la ville.</summary>
    public static Geometry Etoiles => EtoilesChargees.Value;

    // ------------------------------------------------------------------
    // Fabrication
    // ------------------------------------------------------------------

    /// <summary>
    /// Reporte un tracé du carré de composition vers le rectangle du lion.
    /// </summary>
    private static Geometry Reporter(string trace) =>
        Poser(Geometry.Parse(trace), EchelleLion, DecalageLionX, DecalageLionY);

    /// <summary>
    /// Met une forme à l'échelle et la déplace.
    ///
    /// La transformation est portée par un groupe créé ici, et non posée sur
    /// la forme elle-même : une forme issue de <c>Geometry.Parse</c> ou d'un
    /// tracé de texte peut revenir déjà figée, et lui écrire une propriété
    /// lèverait alors une exception au moment de l'affichage — c'est-à-dire
    /// chez le client, sur l'écran de connexion, sans recours.
    /// </summary>
    private static Geometry Poser(Geometry forme, double echelle, double x, double y)
    {
        var groupe = new GeometryGroup
        {
            // Les mèches et les traits se recouvrent : la règle pair-impair
            // y percerait des trous là où le tracé se croise.
            FillRule = FillRule.Nonzero,
            Transform = new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(echelle, echelle),
                    new TranslateTransform(x, y)
                }
            }
        };

        groupe.Children.Add(forme);

        return groupe;
    }

    /// <summary>
    /// Construit un rang de mèches. Chaque mèche part de l'anneau intérieur,
    /// s'élance vers sa pointe et revient : le biais décale la pointe par
    /// rapport à sa base, ce qui donne à la crinière son mouvement au lieu
    /// d'une simple étoile.
    /// </summary>
    /// <param name="nombre">Nombre de mèches.</param>
    /// <param name="rayonInterieur">Rayon de départ des mèches.</param>
    /// <param name="rayonExterieur">Rayon des pointes.</param>
    /// <param name="biaisDegres">Décalage de la pointe, en degrés.</param>
    /// <param name="departDegres">Décalage du rang entier, en degrés.</param>
    /// <param name="souffle">Allongement des mèches du haut, en proportion.</param>
    private static Geometry Criniere(
        int nombre,
        double rayonInterieur,
        double rayonExterieur,
        double biaisDegres,
        double departDegres,
        double souffle)
    {
        // Le resserrement rapproche les points de contrôle du milieu de la
        // mèche : la pointe s'affine au lieu de s'arrondir en pétale.
        const double Resserrement = 0.42d;

        var biais = biaisDegres * Math.PI / 180d;
        var depart = departDegres * Math.PI / 180d;
        var pas = 2d * Math.PI / nombre;
        var rayonControle = rayonInterieur + (rayonExterieur - rayonInterieur) * 0.72d;

        var trace = new StringBuilder("F1 ");

        for (var i = 0; i < nombre; i++)
        {
            var a0 = depart + i * pas;
            var a1 = a0 + pas;
            var am = a0 + pas / 2d;

            // Les mèches tournées vers le haut sont un peu plus longues.
            var allonge = 1d + souffle * Math.Cos(am - 255d * Math.PI / 180d);

            var debut = Polaire(rayonInterieur, a0);
            var fin = Polaire(rayonInterieur, a1);
            var pointe = Polaire(rayonExterieur * allonge, am + biais);
            var controle1 = Polaire(rayonControle, a0 + pas * Resserrement + biais * 0.75d);
            var controle2 = Polaire(rayonControle, a1 - pas * Resserrement + biais * 1.25d);

            trace.Append(CultureInfo.InvariantCulture,
                $"M {debut} Q {controle1} {pointe} Q {controle2} {fin} Z ");
        }

        return Geometry.Parse(trace.ToString());
    }

    /// <summary>Point du repère du lion situé à un rayon et un angle donnés.</summary>
    private static string Polaire(double rayon, double angle) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.##},{1:0.##}",
            CentreX + rayon * Math.Cos(angle),
            CentreY + rayon * Math.Sin(angle));

    /// <summary>Étoile à cinq branches, dans le repère de l'emblème.</summary>
    private static string Etoile(double centreX, double centreY, double rayon)
    {
        var trace = new StringBuilder("F1 M ");

        for (var i = 0; i < 10; i++)
        {
            var angle = (-90d + i * 36d) * Math.PI / 180d;
            var r = i % 2 == 0 ? rayon : rayon * 0.42d;

            trace.Append(CultureInfo.InvariantCulture,
                $"{centreX + r * Math.Cos(angle):0.##},{centreY + r * Math.Sin(angle):0.##}");
            trace.Append(i == 9 ? " Z" : " L ");
        }

        return trace.ToString();
    }

    /// <summary>
    /// Dispose un texte le long d'un cercle, lettre par lettre.
    ///
    /// Chaque lettre est convertie en forme puis tournée pour rester
    /// perpendiculaire au rayon. La largeur réelle de chaque lettre est
    /// mesurée : un « I » occupe donc moins d'arc qu'un « M », faute de quoi
    /// le mot paraîtrait tordu.
    /// </summary>
    /// <param name="texte">Texte à courber.</param>
    /// <param name="rayon">Rayon de la ligne de base.</param>
    /// <param name="centreDegres">Angle où le texte est centré.</param>
    /// <param name="taille">Corps du texte.</param>
    /// <param name="versLInterieur">
    /// Vrai pour le bas de l'anneau, où les lettres pointent vers le centre.
    /// </param>
    /// <param name="espacement">Blanc ajouté entre deux lettres.</param>
    private static Geometry TexteEnArc(
        string texte,
        double rayon,
        double centreDegres,
        double taille,
        bool versLInterieur,
        double espacement)
    {
        var police = new Typeface(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);

        var lettres = new List<(FormattedText Forme, double Arc)>(texte.Length);
        var arcTotal = 0d;

        foreach (var caractere in texte)
        {
            var forme = new FormattedText(
                caractere.ToString(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                police,
                taille,
                Brushes.White,
                1.0d);

            var arc = (forme.Width + espacement) / rayon;

            lettres.Add((forme, arc));
            arcTotal += arc;
        }

        // Nonzero est la règle des contours de police : c'est elle qui laisse
        // le blanc au milieu d'un « O » sans faire disparaître le « B ».
        var groupe = new GeometryGroup { FillRule = FillRule.Nonzero };
        var angle = centreDegres * Math.PI / 180d + (versLInterieur ? arcTotal / 2d : -arcTotal / 2d);

        foreach (var (forme, arc) in lettres)
        {
            var milieu = versLInterieur ? angle - arc / 2d : angle + arc / 2d;

            // La forme est bâtie centrée sur son axe vertical, ligne de base
            // à l'origine : la rotation puis la translation la posent alors
            // exactement sur le cercle.
            var lettre = new GeometryGroup
            {
                FillRule = FillRule.Nonzero,
                Transform = new TransformGroup
                {
                    Children =
                    {
                        new RotateTransform(milieu * 180d / Math.PI + (versLInterieur ? -90d : 90d)),
                        new TranslateTransform(
                            Cote / 2d + rayon * Math.Cos(milieu),
                            Cote / 2d + rayon * Math.Sin(milieu))
                    }
                }
            };

            lettre.Children.Add(forme.BuildGeometry(new Point(-forme.Width / 2d, -forme.Baseline)));

            groupe.Children.Add(lettre);

            angle += versLInterieur ? -arc : arc;
        }

        return groupe;
    }

    /// <summary>
    /// Fige la forme. Une forme figée se partage entre les fenêtres sans
    /// copie et n'est plus modifiable par mégarde.
    /// </summary>
    private static Geometry Figer(Geometry geometrie)
    {
        geometrie.Freeze();

        return geometrie;
    }
}
