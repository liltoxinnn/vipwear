using System.Text.Json;
using System.Text.Json.Serialization;

namespace GestionMagasin.Application.Impression;

/// <summary>Largeur du rouleau de papier de l'imprimante de tickets.</summary>
public enum LargeurRouleau
{
    /// <summary>Rouleau de 58 mm : 48 mm imprimables, 384 points par ligne.</summary>
    Rouleau58 = 58,

    /// <summary>Rouleau de 80 mm : 72 mm imprimables, 576 points par ligne.</summary>
    Rouleau80 = 80
}

/// <summary>
/// Réglages de l'imprimante de tickets d'un poste.
///
/// Ils appartiennent au poste et non au magasin : l'imprimante est branchée
/// sur cet ordinateur-ci, sous un nom que Windows lui a donné ici. Un second
/// poste de caisse aurait la sienne, sous un autre nom. Ces réglages ne vont
/// donc pas dans la base de données, mais dans le dossier de l'utilisateur,
/// à côté du logo.
/// </summary>
public sealed record ReglagesTicket
{
    /// <summary>
    /// Nom de l'imprimante tel que Windows le connaît. Vide tant qu'aucune
    /// n'a été choisie : le logiciel produit alors un PDF, comme avant.
    /// </summary>
    public string? NomImprimante { get; init; }

    public LargeurRouleau Largeur { get; init; } = LargeurRouleau.Rouleau58;

    /// <summary>
    /// Imprime le ticket dès la vente enregistrée, sans poser la question.
    /// Désactivé d'office : un magasin qui n'a jamais imprimé de ticket ne
    /// doit pas se mettre à consommer du papier sans l'avoir décidé.
    /// </summary>
    public bool ImprimerSansDemander { get; init; }

    /// <summary>
    /// Coupe le papier en fin de ticket. Sans effet sur une imprimante qui
    /// n'a pas de massicot : la commande y est simplement ignorée.
    /// </summary>
    public bool CouperPapier { get; init; } = true;

    /// <summary>
    /// Ouvre le tiroir-caisse branché sur l'imprimante. Désactivé d'office :
    /// la plupart des imprimantes de 58 mm n'ont pas de prise de tiroir.
    /// </summary>
    public bool OuvrirTiroir { get; init; }

    /// <summary>Imprime le logo du magasin en tête de ticket, s'il en existe un.</summary>
    public bool ImprimerLogo { get; init; } = true;

    /// <summary>Nombre d'exemplaires imprimés, entre un et trois.</summary>
    public int Exemplaires { get; init; } = 1;

    /// <summary>Vrai lorsqu'une imprimante a été choisie sur ce poste.</summary>
    [JsonIgnore]
    public bool EstConfiguree => !string.IsNullOrWhiteSpace(NomImprimante);

    /// <summary>
    /// Points imprimables par ligne. Ce sont les valeurs de toutes les
    /// imprimantes thermiques à 203 points par pouce : 48 mm sur un rouleau
    /// de 58, 72 mm sur un rouleau de 80.
    /// </summary>
    [JsonIgnore]
    public int PointsParLigne => Largeur == LargeurRouleau.Rouleau80 ? 576 : 384;

    /// <summary>
    /// Caractères par ligne en corps normal : douze points par caractère,
    /// comme la police d'origine de ces imprimantes. Les clients qui ont déjà
    /// vu un ticket de caisse retrouvent la même densité.
    /// </summary>
    [JsonIgnore]
    public int Colonnes => PointsParLigne / 12;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Met les réglages sous forme de texte, pour le fichier du poste.</summary>
    public string VersJson() => JsonSerializer.Serialize(Normaliser(), Options);

    /// <summary>
    /// Relit les réglages du poste.
    ///
    /// Un fichier absent, vide ou abîmé rend les réglages d'origine plutôt
    /// qu'une erreur : la caisse doit pouvoir vendre même si ce fichier a été
    /// effacé ou tronqué par une coupure de courant. Le magasin retombe
    /// alors sur le PDF, et peut rechoisir son imprimante.
    /// </summary>
    public static ReglagesTicket DepuisJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ReglagesTicket();
        }

        try
        {
            return (JsonSerializer.Deserialize<ReglagesTicket>(json, Options) ?? new ReglagesTicket())
                .Normaliser();
        }
        catch (JsonException)
        {
            return new ReglagesTicket();
        }
    }

    /// <summary>Ramène chaque valeur dans ce que le logiciel sait imprimer.</summary>
    public ReglagesTicket Normaliser() => this with
    {
        NomImprimante = string.IsNullOrWhiteSpace(NomImprimante) ? null : NomImprimante.Trim(),
        Largeur = Enum.IsDefined(Largeur) ? Largeur : LargeurRouleau.Rouleau58,
        Exemplaires = Math.Clamp(Exemplaires, 1, 3)
    };
}
