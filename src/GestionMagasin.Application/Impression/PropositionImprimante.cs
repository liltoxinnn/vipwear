using System.Text.RegularExpressions;

namespace GestionMagasin.Application.Impression;

/// <summary>Imprimante proposée d'office, avec la largeur de rouleau qu'on lui suppose.</summary>
public sealed record ImprimanteProposee(string Nom, LargeurRouleau Largeur);

/// <summary>
/// Repère, parmi les imprimantes installées dans Windows, celle qui est
/// probablement l'imprimante de tickets.
///
/// Rien n'est choisi à la place du magasin : la proposition sert seulement
/// à présélectionner la bonne ligne quand on lui demande de choisir. Un poste
/// de caisse a souvent trois ou quatre imprimantes déclarées — celle des
/// tickets, une imprimante de bureau, « Microsoft Print to PDF », OneNote —
/// et la bonne est presque toujours reconnaissable à son nom.
/// </summary>
public static partial class PropositionImprimante
{
    /// <summary>
    /// Noms d'imprimante ou de pilote qui ne sont jamais une imprimante de
    /// tickets : imprimantes virtuelles de Windows et d'Office, et logiciels
    /// qui fabriquent un fichier (PDF24, PDFCreator, CutePDF, doPDF…). Le
    /// mot est cherché partout dans le nom, pas seulement isolé : « PDF24 »
    /// et « CutePDF Writer » sont des imprimantes PDF aussi.
    /// </summary>
    [GeneratedRegex(
        @"pdf|xps|onenote|document writer|\bfax\b|send to|envoyer vers|print to file|imprimer dans un fichier",
        RegexOptions.IgnoreCase)]
    private static partial Regex Virtuelle();

    /// <summary>
    /// Ports des imprimantes qui écrivent un fichier au lieu d'imprimer :
    /// « PORTPROMPT: » (Microsoft Print to PDF et XPS Document Writer, qui
    /// demandent où enregistrer), « XPSPort: », « SHRFAX: » (télécopie),
    /// « nul: » (OneNote), ou un chemin de fichier PDF / XPS. Le port
    /// « FILE: » n'en fait pas partie : branché sur le pilote « Generic /
    /// Text Only », il enregistre les commandes du ticket telles quelles,
    /// ce qui sert à les vérifier sans imprimante.
    /// </summary>
    [GeneratedRegex(@"^(portprompt|xpsport|shrfax|nul):?$|\.(pdf|o?xps)$", RegexOptions.IgnoreCase)]
    private static partial Regex PortVirtuel();

    /// <summary>
    /// Marques sûres d'une imprimante de caisse : « POS », « thermique »,
    /// « ticket », les séries des fabricants (XP-58, TM-T20, RP80, ZJ-5890…),
    /// une largeur en millimètres, ou le pilote « Generic / Text Only » que
    /// le guide recommande à défaut de pilote du fabricant.
    /// </summary>
    [GeneratedRegex(
        @"\bpos\b|pos-?\d|therm|receipt|ticket|re[çc]u|generic\s*/\s*text|" +
        @"\b(xp|tm|rp|zj|gp|pt|mtp|r)-?\s?(t\d+|58|80|5890|8\d{2,3})|(58|80)\s?mm\b|\b(58|80)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex DeCaisse();

    [GeneratedRegex(@"(^|\D)80(\D|$)")]
    private static partial Regex Quatrevingts();

    /// <summary>Propose l'imprimante de tickets, ou rien si aucune ne s'annonce comme telle.</summary>
    public static ImprimanteProposee? Proposer(IEnumerable<string> imprimantes)
    {
        ArgumentNullException.ThrowIfNull(imprimantes);

        var nom = imprimantes
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !EstVirtuelle(n))
            .FirstOrDefault(n => DeCaisse().IsMatch(n));

        return nom is null ? null : new ImprimanteProposee(nom, LargeurSupposee(nom));
    }

    /// <summary>
    /// Vrai pour une imprimante qui fabrique un fichier au lieu d'imprimer :
    /// « Microsoft Print to PDF », « Microsoft XPS Document Writer »,
    /// OneNote, le fax, PDF24…
    ///
    /// Une telle imprimante ne doit jamais recevoir un ticket. Elle accepte
    /// le travail sans erreur, mais ne comprend pas les commandes d'une
    /// imprimante de caisse : elle en tire un fichier PDF vide, de 0 Ko, que
    /// le lecteur PDF déclare endommagé — et le ticket est perdu.
    /// </summary>
    /// <param name="nom">Nom de l'imprimante dans Windows.</param>
    /// <param name="port">Port de l'imprimante, quand on le connaît.</param>
    /// <param name="pilote">Nom de son pilote, quand on le connaît : une imprimante PDF renommée « Caisse » reste une imprimante PDF.</param>
    public static bool EstVirtuelle(string? nom, string? port = null, string? pilote = null) =>
        (!string.IsNullOrWhiteSpace(nom) && Virtuelle().IsMatch(nom))
        || (!string.IsNullOrWhiteSpace(port) && PortVirtuel().IsMatch(port.Trim()))
        || (!string.IsNullOrWhiteSpace(pilote) && Virtuelle().IsMatch(pilote));

    /// <summary>
    /// Largeur lue dans le nom : « 80 » s'il y figure en tant que nombre, 58
    /// sinon — la largeur la plus vendue, et celle que le magasin a achetée.
    /// Un « 1080 » de modèle de bureau n'est pas un 80.
    /// </summary>
    public static LargeurRouleau LargeurSupposee(string nom) =>
        Quatrevingts().IsMatch(nom) ? LargeurRouleau.Rouleau80 : LargeurRouleau.Rouleau58;
}
