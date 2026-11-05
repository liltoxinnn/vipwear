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
    /// Noms qui ne sont jamais une imprimante de tickets : imprimantes
    /// virtuelles de Windows et d'Office.
    /// </summary>
    [GeneratedRegex(@"\b(pdf|xps|onenote|fax|send to|envoyer)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Virtuelle();

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
            .Where(n => !Virtuelle().IsMatch(n))
            .FirstOrDefault(n => DeCaisse().IsMatch(n));

        return nom is null ? null : new ImprimanteProposee(nom, LargeurSupposee(nom));
    }

    /// <summary>
    /// Largeur lue dans le nom : « 80 » s'il y figure en tant que nombre, 58
    /// sinon — la largeur la plus vendue, et celle que le magasin a achetée.
    /// Un « 1080 » de modèle de bureau n'est pas un 80.
    /// </summary>
    public static LargeurRouleau LargeurSupposee(string nom) =>
        Quatrevingts().IsMatch(nom) ? LargeurRouleau.Rouleau80 : LargeurRouleau.Rouleau58;
}
