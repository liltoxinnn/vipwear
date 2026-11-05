namespace GestionMagasin.Application.Impression;

/// <summary>
/// Conversion d'une image en niveaux de gris vers les points d'une tête
/// thermique, qui ne sait que brûler ou laisser blanc.
///
/// Deux traitements coexistent dans un même ticket. Le texte est seuillé :
/// une lettre doit rester une lettre, nette, sans grain. Le logo est tramé :
/// un dégradé seuillé deviendrait une tache, alors qu'une trame en rend les
/// nuances par la densité des points — l'or du lion devient un gris piqué au
/// lieu de disparaître dans le blanc.
/// </summary>
public static class Monochrome
{
    /// <summary>Gris en dessous duquel un point est brûlé.</summary>
    private const int Seuil = 128;

    /// <summary>Octets par rangée de points : huit points par octet, arrondi au-dessus.</summary>
    public static int OctetsParRangee(int largeur) => (largeur + 7) / 8;

    /// <summary>
    /// Convertit une image en points.
    /// </summary>
    /// <param name="gris">Un octet par pixel, de 0 (noir) à 255 (blanc), rangée après rangée.</param>
    /// <param name="largeur">Largeur de l'image en pixels.</param>
    /// <param name="hauteur">Hauteur de l'image en pixels.</param>
    /// <param name="rangeesTramees">
    /// Nombre de rangées, depuis le haut, qui portent le logo et sont donc
    /// tramées ; les suivantes sont seuillées.
    /// </param>
    /// <returns>
    /// Les points, rangée après rangée, huit par octet, le premier point à
    /// gauche dans le bit de poids fort. Un bit à 1 est un point brûlé :
    /// c'est la convention des commandes d'image des imprimantes de caisse.
    /// </returns>
    public static byte[] VersPoints(byte[] gris, int largeur, int hauteur, int rangeesTramees = 0)
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
        var tramees = Math.Clamp(rangeesTramees, 0, hauteur);

        // Erreur de quantification reportée sur les voisins (Floyd-Steinberg).
        // Deux rangées suffisent : la courante et celle du dessous.
        var erreurCourante = new int[largeur + 2];
        var erreurSuivante = new int[largeur + 2];

        for (var y = 0; y < hauteur; y++)
        {
            var trame = y < tramees;

            for (var x = 0; x < largeur; x++)
            {
                int valeur = gris[y * largeur + x];

                if (trame)
                {
                    valeur = Math.Clamp(valeur + erreurCourante[x + 1] / 16, 0, 255);
                }

                var noir = valeur < Seuil;

                if (noir)
                {
                    points[y * pas + x / 8] |= (byte)(0x80 >> (x % 8));
                }

                if (trame)
                {
                    // Un pixel déjà noir ou déjà blanc n'a aucune erreur à
                    // répartir : le texte qui croise le logo reste net.
                    var erreur = valeur - (noir ? 0 : 255);

                    erreurCourante[x + 2] += erreur * 7;
                    erreurSuivante[x] += erreur * 3;
                    erreurSuivante[x + 1] += erreur * 5;
                    erreurSuivante[x + 2] += erreur;
                }
            }

            (erreurCourante, erreurSuivante) = (erreurSuivante, erreurCourante);
            Array.Clear(erreurSuivante);
        }

        return points;
    }
}
