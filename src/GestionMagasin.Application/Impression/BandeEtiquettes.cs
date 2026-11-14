namespace GestionMagasin.Application.Impression;

/// <summary>Image d'une étiquette en niveaux de gris (255 = blanc), et le nombre d'exemplaires voulus.</summary>
public sealed record ImageEtiquette(byte[] Gris, int Largeur, int Hauteur, int Nombre);

/// <summary>
/// Série d'étiquettes pour une imprimante de tickets garnie d'un rouleau
/// d'étiquettes.
///
/// Une imprimante de tickets ne voit pas les étiquettes : elle déroule le
/// rouleau comme du papier ordinaire. Toute la série est donc dessinée en
/// une seule longue image, chaque étiquette exactement un pas plus bas que
/// la précédente — sa hauteur plus l'espace qui la sépare de la suivante —,
/// au point près.
///
/// Le travail avance toujours le papier d'un nombre entier de pas : quand la
/// première série part bien placée, toutes les suivantes le sont aussi, sans
/// rien régler de nouveau. La dernière étiquette ressort au-delà de la barre
/// de découpe, prête à décoller ; l'étiquette suivante, à demi passée sous la
/// tête, reste vierge.
/// </summary>
public static class BandeEtiquettes
{
    /// <summary>Résolution de toutes les imprimantes de tickets thermiques : 8 points par millimètre.</summary>
    public const int Resolution = 203;

    /// <summary>Points par ligne de la tête : 48 mm sur une imprimante de 58, 72 mm sur une de 80.</summary>
    public static int PointsParLigne(TypeImprimanteEtiquettes type) =>
        type == TypeImprimanteEtiquettes.Tickets80 ? 576 : 384;

    /// <summary>Millimètres en points de la tête.</summary>
    public static int Points(double mm) => (int)Math.Round(mm * Resolution / 25.4);

    /// <summary>Pas des étiquettes : une étiquette et l'espace qui la suit.</summary>
    public static int Pas(ReglagesEtiquettes reglages)
    {
        ArgumentNullException.ThrowIfNull(reglages);

        var normalises = reglages.Normaliser();

        return Points(normalises.HauteurMm + normalises.EspaceMm);
    }

    /// <summary>Assemble la série en une seule image, de la largeur de la tête.</summary>
    /// <returns>L'image en niveaux de gris, sa largeur et sa hauteur en points.</returns>
    public static (byte[] Gris, int Largeur, int Hauteur) Assembler(
        IReadOnlyList<ImageEtiquette> etiquettes,
        ReglagesEtiquettes reglages)
    {
        ArgumentNullException.ThrowIfNull(etiquettes);
        ArgumentNullException.ThrowIfNull(reglages);

        reglages = reglages.Normaliser();

        var largeur = PointsParLigne(reglages.TypeEffectif);
        var pas = Pas(reglages);
        var nombre = etiquettes.Sum(e => Math.Max(0, e.Nombre));

        // Avance avant la première étiquette, ramenée à moins d'un pas ; le
        // reste du dernier pas sort la dernière étiquette. Le total fait
        // ainsi un nombre entier de pas.
        var avant = Points(reglages.DecalageVerticalMm) % pas;
        var hauteur = (nombre + 1) * pas;

        var gris = new byte[largeur * hauteur];
        Array.Fill(gris, (byte)255);

        var decalage = Points(reglages.DecalageHorizontalMm);
        var haut = avant;

        foreach (var etiquette in etiquettes)
        {
            var gauche = (largeur - etiquette.Largeur) / 2 + decalage;

            for (var exemplaire = 0; exemplaire < etiquette.Nombre; exemplaire++)
            {
                Copier(etiquette, gris, largeur, hauteur, gauche, haut);
                haut += pas;
            }
        }

        return (gris, largeur, hauteur);
    }

    /// <summary>
    /// Copie une étiquette à sa place. Ce qui dépasse de la tête est perdu :
    /// un décalage trop fort coupe le bord, il ne déborde jamais ailleurs.
    /// </summary>
    private static void Copier(ImageEtiquette etiquette, byte[] gris, int largeur, int hauteur, int gauche, int haut)
    {
        for (var rangee = 0; rangee < etiquette.Hauteur; rangee++)
        {
            var y = haut + rangee;

            if (y < 0 || y >= hauteur)
            {
                continue;
            }

            for (var colonne = 0; colonne < etiquette.Largeur; colonne++)
            {
                var x = gauche + colonne;

                if (x >= 0 && x < largeur)
                {
                    gris[y * largeur + x] = etiquette.Gris[rangee * etiquette.Largeur + colonne];
                }
            }
        }
    }
}
