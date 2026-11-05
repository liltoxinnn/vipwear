using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GestionMagasin.Application.Impression;

namespace GestionMagasin.App.Impression;

/// <summary>Image d'un ticket en niveaux de gris, prête à être convertie en points.</summary>
/// <param name="Gris">Un octet par pixel, de 0 (noir) à 255 (blanc).</param>
/// <param name="RangeesLogo">Rangées occupées par le logo, depuis le haut, à tramer.</param>
internal sealed record ImageTicket(byte[] Gris, int Largeur, int Hauteur, int RangeesLogo);

/// <summary>
/// Dessin du ticket, à la résolution exacte de la tête d'impression : un
/// pixel est un point. Rien n'est redimensionné ensuite — ni par Windows ni
/// par l'imprimante — et le texte sort aussi net que l'imprimante sait le
/// faire.
/// </summary>
internal static class RenduTicket
{
    /// <summary>
    /// Police à chasse fixe, présente sur tous les Windows depuis Vista. Les
    /// deux suivantes ne servent que si elle a été retirée ; elles sont aussi
    /// à chasse fixe, et l'alignement des montants tient.
    /// </summary>
    private static readonly FontFamily Police = new("Consolas, Lucida Console, Courier New");

    /// <summary>Part de la largeur du papier occupée par le logo.</summary>
    private const double LargeurLogo = 0.5;

    /// <summary>Blanc laissé au-dessus, entre le logo et le texte, et sous le texte.</summary>
    private const int Marge = 8;

    /// <summary>Interligne ajouté sous chaque ligne, en points.</summary>
    private const int Interligne = 2;

    /// <summary>Dessine le ticket.</summary>
    /// <param name="lignes">Lignes déjà composées à la largeur du rouleau.</param>
    /// <param name="reglages">Réglages du poste.</param>
    /// <param name="logo">Logo du magasin, ou rien.</param>
    public static ImageTicket Dessiner(
        IReadOnlyList<LigneTicket> lignes,
        ReglagesTicket reglages,
        BitmapSource? logo)
    {
        var largeur = reglages.PointsParLigne;
        var colonnes = reglages.Colonnes;
        var grandes = CompositionTicket.ColonnesGrandes(colonnes);

        var normale = new Typeface(Police, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var grasse = new Typeface(Police, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        // Le corps est calculé pour que la ligne pleine remplisse le papier :
        // trente-deux caractères sur 384 points, soit douze points chacun.
        // Un centième est retenu : les avances ne tombent pas toujours sur un
        // point entier, et leur cumul ne doit pas couper le dernier montant.
        const double Reserve = 0.99;

        var corpsNormal = CorpsPour(normale, Reserve * largeur / colonnes);
        var corpsGras = CorpsPour(grasse, Reserve * largeur / colonnes);
        var corpsGrand = CorpsPour(grasse, Reserve * largeur / grandes);

        // ---------- Mise en place ----------

        var textes = new List<(FormattedText Texte, double Y)>();
        var y = (double)Marge;

        Rect? cadreLogo = null;

        if (logo is not null && logo.PixelWidth > 0 && logo.PixelHeight > 0)
        {
            var largeurLogo = Math.Round(largeur * LargeurLogo);
            var hauteurLogo = Math.Round(largeurLogo * logo.PixelHeight / logo.PixelWidth);

            cadreLogo = new Rect((largeur - largeurLogo) / 2, y, largeurLogo, hauteurLogo);
            y += hauteurLogo + Marge;
        }

        var rangeesLogo = (int)Math.Ceiling(y);

        foreach (var ligne in lignes)
        {
            var (police, corps) = ligne.Style switch
            {
                StyleLigne.Grand => (grasse, corpsGrand),
                StyleLigne.Gras => (grasse, corpsGras),
                _ => (normale, corpsNormal)
            };

            // Une ligne vide garde la hauteur d'une ligne : c'est un blanc voulu.
            var texte = Formater(ligne.Texte.Length == 0 ? " " : ligne.Texte, police, corps);

            textes.Add((texte, y));
            y += Math.Ceiling(texte.Height) + Interligne;
        }

        var hauteur = (int)Math.Ceiling(y) + Marge;

        // ---------- Dessin ----------

        var visuel = new DrawingVisual();

        // Le logo est réduit avec soin : il est fourni en grand et ramené à
        // moins de deux cents points.
        RenderOptions.SetBitmapScalingMode(visuel, BitmapScalingMode.HighQuality);

        // Lettres sans lissage : la tête ne connaît que le noir et le blanc,
        // et un bord à demi gris se perdrait au seuillage.
        TextOptions.SetTextRenderingMode(visuel, TextRenderingMode.Aliased);

        using (var contexte = visuel.RenderOpen())
        {
            contexte.DrawRectangle(Brushes.White, null, new Rect(0, 0, largeur, hauteur));

            if (cadreLogo is { } cadre)
            {
                contexte.DrawImage(logo, cadre);
            }

            foreach (var (texte, position) in textes)
            {
                contexte.DrawText(texte, new Point(0, position));
            }
        }

        var image = new RenderTargetBitmap(largeur, hauteur, 96, 96, PixelFormats.Pbgra32);
        image.Render(visuel);

        var pixels = new byte[largeur * hauteur * 4];
        image.CopyPixels(pixels, largeur * 4, 0);

        // Le fond est blanc et opaque partout : les composantes peuvent être
        // lues telles quelles, sans tenir compte de la transparence.
        var gris = new byte[largeur * hauteur];

        for (var i = 0; i < gris.Length; i++)
        {
            var bleu = pixels[i * 4];
            var vert = pixels[i * 4 + 1];
            var rouge = pixels[i * 4 + 2];

            gris[i] = (byte)((rouge * 299 + vert * 587 + bleu * 114) / 1000);
        }

        return new ImageTicket(gris, largeur, hauteur, cadreLogo is null ? 0 : rangeesLogo);
    }

    /// <summary>
    /// Corps de police donnant l'avance voulue par caractère. La mesure est
    /// faite sur la police réellement trouvée sur le poste : si Consolas
    /// manque, la police de secours est mesurée à son tour, et la ligne tient
    /// toujours dans le papier.
    /// </summary>
    private static double CorpsPour(Typeface police, double avance)
    {
        const double CorpsDeMesure = 100;
        const string Echantillon = "0000000000";

        var mesure = Formater(Echantillon, police, CorpsDeMesure);
        var avanceMesuree = mesure.WidthIncludingTrailingWhitespace / Echantillon.Length;

        return CorpsDeMesure * avance / avanceMesuree;
    }

    private static FormattedText Formater(string texte, Typeface police, double corps) =>
        new(texte,
            CultureInfo.GetCultureInfo("fr-FR"),
            FlowDirection.LeftToRight,
            police,
            corps,
            Brushes.Black,
            pixelsPerDip: 1.0);
}
