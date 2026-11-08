using QRCoder;

namespace GestionMagasin.Application.Impression;

/// <summary>
/// QR code imprimé en bas du ticket.
///
/// Il est fabriqué à partir de son lien, et non recopié d'une image : une
/// image réduite pour tenir sur le ticket voit ses petits carrés tomber
/// entre deux points de la tête d'impression, et sort pâteuse — un téléphone
/// ne la lit plus. Fabriqué ici, chaque carré mesure un nombre entier de
/// points et sort net.
/// </summary>
public static class CodeQr
{
    /// <summary>
    /// Longueur maximale du lien. Au-delà, le QR code grossit au point de ne
    /// plus être « petit », et ses carrés deviennent trop fins pour une tête
    /// thermique.
    /// </summary>
    public const int LongueurMaximale = 200;

    /// <summary>
    /// Marge blanche exigée autour du code, en carrés. Sans elle, les
    /// téléphones ne repèrent pas les trois coins du code.
    /// </summary>
    public const int ZoneDeSilence = 4;

    /// <summary>
    /// Carrés du code, sans la marge : vrai pour un carré noir. La correction
    /// d'erreur moyenne (15 %) laisse lire un code légèrement taché ou plié,
    /// ce qui arrive à un ticket glissé dans une poche.
    /// </summary>
    public static bool[,] Matrice(string contenu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contenu);

        using var generateur = new QRCodeGenerator();
        using var donnees = generateur.CreateQrCode(contenu.Trim(), QRCodeGenerator.ECCLevel.M);

        // La bibliothèque entoure le code de sa zone de silence : elle est
        // retirée ici, le ticket la ménage lui-même avec ses marges.
        var lignes = donnees.ModuleMatrix;
        var cote = lignes.Count - 2 * ZoneDeSilence;
        var matrice = new bool[cote, cote];

        for (var y = 0; y < cote; y++)
        {
            for (var x = 0; x < cote; x++)
            {
                matrice[y, x] = lignes[y + ZoneDeSilence][x + ZoneDeSilence];
            }
        }

        return matrice;
    }

    /// <summary>
    /// Points d'impression par carré du code. Quatre points font un carré
    /// d'un demi-millimètre, que tous les téléphones lisent ; un code plus
    /// grand, d'un lien plus long, descend à trois pour rester petit, jamais
    /// en dessous.
    /// </summary>
    public static int PointsParCarre(int cote) => cote <= 37 ? 4 : 3;

    /// <summary>Image PNG du code, pour les documents PDF.</summary>
    public static byte[] Png(string contenu, int pixelsParCarre = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contenu);

        using var generateur = new QRCodeGenerator();
        using var donnees = generateur.CreateQrCode(contenu.Trim(), QRCodeGenerator.ECCLevel.M);

        return new PngByteQRCode(donnees).GetGraphic(pixelsParCarre);
    }
}
