using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GestionMagasin.Application.Impression;

namespace GestionMagasin.App.Impression;

/// <summary>
/// Dessin d'une étiquette code-barres, un pixel par point de l'imprimante.
///
/// L'image est rendue en noir et blanc purs, à la résolution de
/// l'imprimante : le pilote n'a plus rien à réduire ni à tramer, et chaque
/// barre sort exactement à la largeur calculée. Une barre adoucie de gris
/// serait tramée par le pilote, et la douchette ne la lirait plus.
/// </summary>
internal static class RenduEtiquette
{
    /// <summary>Police sans empattements, lisible en petit corps sur une tête thermique.</summary>
    private static readonly FontFamily Police = new("Arial, Segoe UI");

    private static readonly Typeface Normale = new(Police, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static readonly Typeface Grasse = new(Police, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    /// <summary>Le texte trop long ne rapetisse pas au-delà : plus petit, il ne se lirait plus.</summary>
    private const double ReductionMaximale = 0.75;

    /// <summary>
    /// Met le texte dans sa case. Trop long — un nom d'article, une couleur
    /// à rallonge —, il rapetisse d'abord un peu, puis se termine par « … »
    /// plutôt que de déborder sur le bord de l'étiquette ou sur le prix.
    /// </summary>
    /// <returns>Le texte prêt à dessiner, et de combien le descendre pour qu'un texte réduit reste sur sa ligne.</returns>
    private static (FormattedText Texte, double Decalage) Ajuster(TexteEtiquette ligne)
    {
        var police = ligne.Gras ? Grasse : Normale;
        var corps = (double)ligne.Corps;
        var minimum = Math.Max(1, ligne.Corps * ReductionMaximale);

        var texte = Formater(ligne.Texte, police, corps);

        while (texte.WidthIncludingTrailingWhitespace > ligne.Largeur && corps - 1 >= minimum)
        {
            corps -= 1;
            texte = Formater(ligne.Texte, police, corps);
        }

        texte.MaxTextWidth = Math.Max(1, ligne.Largeur);
        texte.MaxLineCount = 1;
        texte.Trimming = TextTrimming.CharacterEllipsis;
        texte.TextAlignment = ligne.Alignement switch
        {
            AlignementTexte.Gauche => TextAlignment.Left,
            AlignementTexte.Droite => TextAlignment.Right,
            _ => TextAlignment.Center
        };

        return (texte, ligne.Corps - corps);
    }

    private static FormattedText Formater(string texte, Typeface police, double corps) =>
        new(texte,
            CultureInfo.GetCultureInfo("fr-FR"),
            FlowDirection.LeftToRight,
            police,
            corps,
            Brushes.Black,
            pixelsPerDip: 1.0);

    /// <summary>Dessine l'étiquette, prête à imprimer à <paramref name="pointsParPouce"/>.</summary>
    public static BitmapSource Dessiner(MiseEnPageEtiquette etiquette, int pointsParPouce)
    {
        var largeur = etiquette.Largeur;
        var hauteur = etiquette.Hauteur;

        var visuel = new DrawingVisual();

        // Ni lissage des lettres ni bords à demi gris : la tête ne connaît
        // que le noir et le blanc.
        TextOptions.SetTextRenderingMode(visuel, TextRenderingMode.Aliased);
        RenderOptions.SetEdgeMode(visuel, EdgeMode.Aliased);

        using (var contexte = visuel.RenderOpen())
        {
            contexte.DrawRectangle(Brushes.White, null, new Rect(0, 0, largeur, hauteur));

            foreach (var barre in etiquette.Barres)
            {
                contexte.DrawRectangle(Brushes.Black, null, new Rect(barre.X, barre.Y, barre.Largeur, barre.Hauteur));
            }

            foreach (var ligne in etiquette.Textes)
            {
                var (texte, decalage) = Ajuster(ligne);

                contexte.DrawText(texte, new Point(ligne.X, ligne.Haut + decalage));
            }
        }

        var rendu = new RenderTargetBitmap(largeur, hauteur, 96, 96, PixelFormats.Pbgra32);
        rendu.Render(visuel);

        var pixels = new byte[largeur * hauteur * 4];
        rendu.CopyPixels(pixels, largeur * 4, 0);

        var gris = new byte[largeur * hauteur];

        for (var i = 0; i < gris.Length; i++)
        {
            // Fond blanc opaque : les composantes se lisent sans tenir compte
            // de l'opacité. Seuil au milieu : noir ou blanc, rien entre deux.
            var luminance = (pixels[i * 4] * 114 + pixels[i * 4 + 1] * 587 + pixels[i * 4 + 2] * 299) / 1000;
            gris[i] = luminance < 128 ? (byte)0 : (byte)255;
        }

        var image = BitmapSource.Create(
            largeur, hauteur, pointsParPouce, pointsParPouce, PixelFormats.Gray8, null, gris, largeur);

        image.Freeze();

        return image;
    }
}
