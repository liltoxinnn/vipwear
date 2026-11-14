using System.Text.Json;
using System.Text.Json.Serialization;

namespace GestionMagasin.Application.Impression;

/// <summary>Sorte d'imprimante sur laquelle sortent les étiquettes.</summary>
public enum TypeImprimanteEtiquettes
{
    /// <summary>
    /// Imprimante d'étiquettes (Xprinter, TSC, Zebra…) : elle repère elle-même
    /// chaque étiquette, et l'impression passe par son pilote Windows.
    /// </summary>
    Etiquettes,

    /// <summary>
    /// Imprimante de tickets de 58 mm garnie d'un rouleau d'étiquettes : elle
    /// ne voit pas les étiquettes, le logiciel compte lui-même l'avance.
    /// </summary>
    Tickets58,

    /// <summary>Imprimante de tickets de 80 mm garnie d'un rouleau d'étiquettes.</summary>
    Tickets80
}

/// <summary>
/// Réglages de l'imprimante d'étiquettes code-barres d'un poste.
///
/// Comme ceux de l'imprimante de tickets, ils appartiennent au poste :
/// l'imprimante est branchée sur cet ordinateur, sous le nom que Windows lui
/// a donné ici. Ils sont enregistrés dans le dossier de l'utilisateur, pas
/// dans la base.
/// </summary>
public sealed record ReglagesEtiquettes
{
    /// <summary>Largeur des étiquettes du magasin.</summary>
    public const int LargeurParDefaut = 40;

    /// <summary>Hauteur des étiquettes du magasin : des étiquettes de 40 × 20 mm.</summary>
    public const int HauteurParDefaut = 20;

    public const int DimensionMinimale = 20;

    public const int DimensionMaximale = 110;

    /// <summary>
    /// Version du fichier des réglages. La première retenait 40 × 30 mm,
    /// l'ancienne taille proposée d'office ; le magasin utilise des
    /// étiquettes de 40 × 20.
    /// </summary>
    public const int VersionActuelle = 3;

    /// <summary>Version du fichier relu ; zéro pour un fichier d'avant la numérotation.</summary>
    public int Version { get; init; }

    /// <summary>Nom de l'imprimante d'étiquettes tel que Windows le connaît.</summary>
    public string? NomImprimante { get; init; }

    /// <summary>Largeur d'une étiquette, en millimètres.</summary>
    public int LargeurMm { get; init; } = LargeurParDefaut;

    /// <summary>Hauteur d'une étiquette, en millimètres, sans l'espace entre deux étiquettes.</summary>
    public int HauteurMm { get; init; } = HauteurParDefaut;

    /// <summary>Imprime le prix de vente sur l'étiquette.</summary>
    public bool AfficherPrix { get; init; } = true;

    /// <summary>Imprime le nom du magasin en tête d'étiquette.</summary>
    public bool AfficherMagasin { get; init; } = true;

    /// <summary>
    /// Imprime la taille et la couleur. Retirées d'office : sur une étiquette
    /// de 40 × 20, le magasin préfère un prix bien lisible.
    /// </summary>
    public bool AfficherDetail { get; init; }

    /// <summary>Sorte d'imprimante ; rien tant qu'elle est devinée d'après son nom.</summary>
    public TypeImprimanteEtiquettes? Type { get; init; }

    /// <summary>
    /// Espace entre deux étiquettes du rouleau, en millimètres. Une
    /// imprimante de tickets ne le voit pas : il faut le lui dire, au
    /// dixième près, sinon chaque étiquette glisse un peu plus que la
    /// précédente.
    /// </summary>
    public double EspaceMm { get; init; } = EspaceParDefaut;

    /// <summary>
    /// Imprimante de tickets : décalage de l'impression vers la droite (positif)
    /// ou la gauche (négatif), en millimètres, à partir du milieu de la tête.
    /// </summary>
    public double DecalageHorizontalMm { get; init; }

    /// <summary>
    /// Imprimante de tickets : avance avant la première étiquette, en
    /// millimètres. Nulle d'office : l'impression commence là où est le
    /// papier, sans blanc. ▲ et ▼ la corrigent si les étiquettes sortent
    /// trop bas ou trop haut.
    /// </summary>
    public double DecalageVerticalMm { get; init; } = DecalageVerticalParDefaut;

    public const double EspaceParDefaut = 2.0;

    /// <summary>Aucun blanc avant la première étiquette.</summary>
    public const double DecalageVerticalParDefaut = 0;

    /// <summary>
    /// Ancienne avance d'office : elle supposait un espace entre deux
    /// étiquettes au ras de la barre de découpe, et faisait sortir chaque
    /// étiquette 11 mm trop bas.
    /// </summary>
    private const double AncienDecalageVertical = 11.0;

    /// <summary>Sorte d'imprimante retenue, ou devinée d'après son nom.</summary>
    [JsonIgnore]
    public TypeImprimanteEtiquettes TypeEffectif => Type ?? Deviner(NomImprimante);

    /// <summary>Vrai quand les étiquettes sortent d'une imprimante de tickets.</summary>
    [JsonIgnore]
    public bool SurImprimanteDeTickets => TypeEffectif != TypeImprimanteEtiquettes.Etiquettes;

    /// <summary>
    /// Sorte d'imprimante d'après son nom : « POS-58 R58P » est une imprimante
    /// de tickets de 58 mm, « Xprinter XP-365B » une imprimante d'étiquettes.
    /// </summary>
    public static TypeImprimanteEtiquettes Deviner(string? nomImprimante) =>
        !PropositionImprimante.EstImprimanteDeCaisse(nomImprimante)
            ? TypeImprimanteEtiquettes.Etiquettes
            : PropositionImprimante.LargeurSupposee(nomImprimante!) == LargeurRouleau.Rouleau80
                ? TypeImprimanteEtiquettes.Tickets80
                : TypeImprimanteEtiquettes.Tickets58;

    /// <summary>Vrai lorsqu'une imprimante d'étiquettes a été choisie sur ce poste.</summary>
    [JsonIgnore]
    public bool EstConfiguree => !string.IsNullOrWhiteSpace(NomImprimante);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Met les réglages sous forme de texte, pour le fichier du poste.</summary>
    public string VersJson() => JsonSerializer.Serialize(Normaliser(), Options);

    /// <summary>
    /// Relit les réglages du poste. Un fichier absent ou abîmé rend les
    /// réglages d'origine plutôt qu'une erreur.
    /// </summary>
    public static ReglagesEtiquettes DepuisJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ReglagesEtiquettes();
        }

        try
        {
            var lus = JsonSerializer.Deserialize<ReglagesEtiquettes>(json, Options) ?? new ReglagesEtiquettes();

            // 40 × 30 était la taille proposée d'office par la première
            // version, retenue telle quelle à la première impression : sur les
            // étiquettes de 40 × 20 du magasin, chaque impression débordait
            // sur l'étiquette suivante. Une taille choisie depuis est gardée.
            if (lus.Version < 2 && lus is { LargeurMm: 40, HauteurMm: 30 })
            {
                lus = lus with { HauteurMm = HauteurParDefaut };
            }

            // L'avance de 11 mm retenue par la version précédente n'avait été
            // choisie par personne : elle disparaît.
            if (lus.Version < 3 && Math.Abs(lus.DecalageVerticalMm - AncienDecalageVertical) < 0.05)
            {
                lus = lus with { DecalageVerticalMm = DecalageVerticalParDefaut };
            }

            return lus.Normaliser();
        }
        catch (JsonException)
        {
            return new ReglagesEtiquettes();
        }
    }

    /// <summary>Ramène chaque valeur dans ce que le logiciel sait imprimer.</summary>
    public ReglagesEtiquettes Normaliser() => this with
    {
        Version = VersionActuelle,
        NomImprimante = string.IsNullOrWhiteSpace(NomImprimante) ? null : NomImprimante.Trim(),
        LargeurMm = Math.Clamp(LargeurMm, DimensionMinimale, DimensionMaximale),
        HauteurMm = Math.Clamp(HauteurMm, DimensionMinimale, DimensionMaximale),
        Type = Type is { } type && Enum.IsDefined(type) ? type : null,
        EspaceMm = Borner(EspaceMm, 0, 10, EspaceParDefaut),
        DecalageHorizontalMm = Borner(DecalageHorizontalMm, -20, 20, 0),
        DecalageVerticalMm = Borner(DecalageVerticalMm, 0, 40, DecalageVerticalParDefaut)
    };

    /// <summary>Valeur au dixième de millimètre, dans ses bornes ; la valeur d'origine si elle est illisible.</summary>
    private static double Borner(double valeur, double minimum, double maximum, double origine) =>
        double.IsFinite(valeur) ? Math.Round(Math.Clamp(valeur, minimum, maximum), 1) : origine;
}
