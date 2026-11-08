namespace GestionMagasin.Application.Impression;

/// <summary>
/// Commandes ESC/POS envoyées à l'imprimante de tickets.
///
/// ESC/POS est le langage commun des imprimantes de caisse : Epson l'a
/// défini, et les imprimantes de 58 mm vendues sous des dizaines de marques
/// le reprennent. Le ticket n'y est pas envoyé en texte mais en image : le
/// texte dépendrait de la table de caractères que chaque fabricant a gravée
/// dans son imprimante, et « é » s'y imprime parfois « Ú ». Une image est la
/// même partout — accents, symboles et logo compris.
/// </summary>
public static class EscPos
{
    /// <summary>
    /// Rangées de points envoyées par commande d'image. Les petites
    /// imprimantes ont une mémoire de réception réduite : un ticket entier
    /// d'un seul bloc peut la dépasser, et la fin du ticket se perd. Par
    /// bandes, chaque morceau tient.
    /// </summary>
    public const int RangeesParBande = 128;

    /// <summary>Lignes de papier avancées après le ticket, pour le dégager de la barre de découpe.</summary>
    public const byte AvanceFinale = 3;

    /// <summary>ESC @ : remet l'imprimante dans son état de départ.</summary>
    public static readonly byte[] Initialiser = [0x1B, 0x40];

    /// <summary>
    /// ESC p 0 25 250 : impulsion sur la prise du tiroir-caisse, broche 2,
    /// 50 ms de travail puis 500 ms de repos — les valeurs que les tiroirs
    /// du marché acceptent tous.
    /// </summary>
    public static readonly byte[] OuvrirTiroir = [0x1B, 0x70, 0x00, 0x19, 0xFA];

    /// <summary>
    /// GS V 66 0 : avance jusqu'au massicot puis coupe en laissant une
    /// attache. Ignorée par une imprimante sans massicot.
    /// </summary>
    public static readonly byte[] Couper = [0x1D, 0x56, 0x42, 0x00];

    /// <summary>ESC d n : avance le papier de n lignes.</summary>
    public static byte[] Avancer(byte lignes) => [0x1B, 0x64, lignes];

    /// <summary>
    /// Compose le travail d'impression complet d'un ticket.
    /// </summary>
    /// <param name="points">Image en points, telle que la rend <see cref="Monochrome.VersPoints"/>.</param>
    /// <param name="largeur">Largeur de l'image en points.</param>
    /// <param name="hauteur">Hauteur de l'image en points.</param>
    /// <param name="reglages">Réglages du poste : coupe, tiroir, exemplaires.</param>
    public static byte[] Ticket(byte[] points, int largeur, int hauteur, ReglagesTicket reglages)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(reglages);

        var pas = Monochrome.OctetsParRangee(largeur);

        if (points.Length < pas * hauteur)
        {
            throw new ArgumentException("L'image est plus courte que ses dimensions.", nameof(points));
        }

        using var flux = new MemoryStream();

        flux.Write(Initialiser);

        // Le tiroir s'ouvre avant l'impression : le caissier rend la monnaie
        // pendant que le ticket sort, au lieu de l'attendre.
        if (reglages.OuvrirTiroir)
        {
            flux.Write(OuvrirTiroir);
        }

        var exemplaires = Math.Clamp(reglages.Exemplaires, 1, 3);

        for (var exemplaire = 0; exemplaire < exemplaires; exemplaire++)
        {
            for (var debut = 0; debut < hauteur; debut += RangeesParBande)
            {
                var rangees = Math.Min(RangeesParBande, hauteur - debut);

                flux.Write(EnteteImage(pas, rangees));
                flux.Write(points, debut * pas, rangees * pas);
            }

            // L'avance a toujours lieu : sur une imprimante sans massicot, la
            // coupe serait ignorée et la fin du ticket resterait sous la barre
            // de découpe, déchirée avec le papier.
            flux.Write(Avancer(AvanceFinale));

            if (reglages.CouperPapier)
            {
                flux.Write(Couper);
            }
        }

        return flux.ToArray();
    }

    /// <summary>
    /// GS v 0 : image en points, mode normal. Suivent la largeur en octets
    /// et la hauteur en rangées, chacune sur deux octets, poids faible
    /// d'abord.
    /// </summary>
    public static byte[] EnteteImage(int octetsParRangee, int rangees) =>
    [
        0x1D, 0x76, 0x30, 0x00,
        (byte)(octetsParRangee & 0xFF), (byte)(octetsParRangee >> 8),
        (byte)(rangees & 0xFF), (byte)(rangees >> 8)
    ];
}
