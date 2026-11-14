using System.Text.Json;
using System.Text.Json.Serialization;

namespace GestionMagasin.Application.Impression;

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

    /// <summary>Vrai lorsqu'une imprimante d'étiquettes a été choisie sur ce poste.</summary>
    [JsonIgnore]
    public bool EstConfiguree => !string.IsNullOrWhiteSpace(NomImprimante);

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

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
            return (JsonSerializer.Deserialize<ReglagesEtiquettes>(json, Options) ?? new ReglagesEtiquettes())
                .Normaliser();
        }
        catch (JsonException)
        {
            return new ReglagesEtiquettes();
        }
    }

    /// <summary>Ramène chaque valeur dans ce que le logiciel sait imprimer.</summary>
    public ReglagesEtiquettes Normaliser() => this with
    {
        NomImprimante = string.IsNullOrWhiteSpace(NomImprimante) ? null : NomImprimante.Trim(),
        LargeurMm = Math.Clamp(LargeurMm, DimensionMinimale, DimensionMaximale),
        HauteurMm = Math.Clamp(HauteurMm, DimensionMinimale, DimensionMaximale)
    };
}
