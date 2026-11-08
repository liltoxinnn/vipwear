namespace GestionMagasin.Application.Impression;

/// <summary>
/// Conversion d'une image en niveaux de gris vers les points d'une tête
/// thermique, qui ne sait que brûler ou laisser blanc.
///
/// Tout est seuillé : chaque pixel devient noir ou blanc selon qu'il est
/// plus sombre ou plus clair qu'un gris de référence. Le texte l'est au gris
/// moyen — il est déjà noir sur blanc. Le logo l'est à un gris choisi
/// d'après le logo lui-même.
///
/// Une trame — des points dont la densité imite le gris — avait d'abord été
/// retenue pour le logo. Essayée sur celui du magasin, elle a changé le lion
/// doré en une bouillie de points gris et semé du bruit dans le disque noir.
/// Un logo est un dessin à quelques teintes franches, pas une photographie :
/// réduit à deux tons, il sort comme un tampon, net.
/// </summary>
public static class Monochrome
{
    /// <summary>Gris en dessous duquel un point de texte est brûlé.</summary>
    private const int Seuil = 128;

    /// <summary>Octets par rangée de points : huit points par octet, arrondi au-dessus.</summary>
    public static int OctetsParRangee(int largeur) => (largeur + 7) / 8;

    /// <summary>
    /// Convertit une image en points.
    /// </summary>
    /// <param name="gris">Un octet par pixel, de 0 (noir) à 255 (blanc), rangée après rangée.</param>
    /// <param name="largeur">Largeur de l'image en pixels.</param>
    /// <param name="hauteur">Hauteur de l'image en pixels.</param>
    /// <param name="rangeesLogo">
    /// Nombre de rangées, depuis le haut, qui portent le logo : elles sont
    /// seuillées au gris propre au logo, les suivantes au gris moyen.
    /// </param>
    /// <returns>
    /// Les points, rangée après rangée, huit par octet, le premier point à
    /// gauche dans le bit de poids fort. Un bit à 1 est un point brûlé :
    /// c'est la convention des commandes d'image des imprimantes de caisse.
    /// </returns>
    public static byte[] VersPoints(byte[] gris, int largeur, int hauteur, int rangeesLogo = 0)
    {
        ArgumentNullException.ThrowIfNull(gris);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(largeur);
        ArgumentOutOfRangeException.ThrowIfNegative(hauteur);

        if (gris.Length < largeur * hauteur)
        {
            throw new ArgumentException(
                $"L'image annonce {largeur} × {hauteur} pixels mais n'en contient que {gris.Length}.",
                nameof(gris));
        }

        var pas = OctetsParRangee(largeur);
        var points = new byte[pas * hauteur];
        var logo = Math.Clamp(rangeesLogo, 0, hauteur);
        var seuilLogo = logo > 0 ? SeuilDuLogo(gris, largeur, logo) : Seuil;

        for (var y = 0; y < hauteur; y++)
        {
            var seuil = y < logo ? seuilLogo : Seuil;

            for (var x = 0; x < largeur; x++)
            {
                if (gris[y * largeur + x] < seuil)
                {
                    points[y * pas + x / 8] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        return points;
    }

    /// <summary>
    /// Gris qui sépare le mieux les teintes sombres et les teintes claires du
    /// logo (méthode d'Otsu) : celui pour lequel les deux groupes de pixels
    /// sont chacun le plus homogène.
    ///
    /// Un seuil fixe ne conviendrait pas à tous les logos. Sur celui du
    /// magasin — lion doré sur disque noir — l'or doit devenir blanc, sans
    /// quoi le lion se fond dans le disque. Sur un logo doré posé sur du
    /// blanc, le même or doit devenir noir, sans quoi il disparaît dans le
    /// papier. Le gris qui sépare les deux groupes, lui, tombe juste dans
    /// les deux cas.
    /// </summary>
    /// <returns>Un pixel est brûlé s'il est strictement plus sombre que la valeur rendue.</returns>
    public static int SeuilDuLogo(byte[] gris, int largeur, int rangees)
    {
        var total = largeur * rangees;
        var histogramme = new long[256];

        for (var i = 0; i < total; i++)
        {
            histogramme[gris[i]]++;
        }

        double somme = 0;

        for (var teinte = 0; teinte < 256; teinte++)
        {
            somme += teinte * (double)histogramme[teinte];
        }

        double sommeSombres = 0;
        long sombres = 0;
        var meilleureSeparation = -1d;
        var seuil = Seuil;

        for (var teinte = 0; teinte < 256; teinte++)
        {
            sombres += histogramme[teinte];

            if (sombres == 0)
            {
                continue;
            }

            var claires = total - sombres;

            // Une image d'une seule teinte n'a rien à séparer : le gris moyen
            // est gardé.
            if (claires == 0)
            {
                break;
            }

            sommeSombres += teinte * (double)histogramme[teinte];

            var moyenneSombres = sommeSombres / sombres;
            var moyenneClaires = (somme - sommeSombres) / claires;
            var ecart = moyenneSombres - moyenneClaires;
            var separation = (double)sombres * claires * ecart * ecart;

            if (separation > meilleureSeparation)
            {
                meilleureSeparation = separation;
                seuil = teinte + 1;
            }
        }

        return seuil;
    }
}
