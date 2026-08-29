using System.Globalization;

namespace GestionMagasin.Application.Services;

/// <summary>
/// Saisons d'une collection.
///
/// Les quatre saisons sont proposées d'office : sans elles, la liste d'un
/// magasin qui vient d'ouvrir est vide, et le vendeur doit deviner
/// l'orthographe attendue. Chacun écrirait « ete », « Eté », « ÉTÉ », et le
/// filtre par saison ne regrouperait plus rien.
///
/// La saisie libre reste possible : un magasin peut vouloir « Ramadan » ou
/// « Aïd ». Ces valeurs rejoignent la liste dès qu'un produit les porte, mais
/// après les quatre saisons, qui restent en tête dans l'ordre du calendrier.
/// </summary>
public static class Saisons
{
    /// <summary>Les quatre saisons, dans l'ordre du calendrier.</summary>
    public static readonly IReadOnlyList<string> Standard =
        ["Printemps", "Été", "Automne", "Hiver"];

    /// <summary>
    /// Compose la liste proposée : les quatre saisons d'abord, puis les
    /// saisons libres déjà employées dans le catalogue, sans doublon.
    /// </summary>
    /// <param name="employees">Saisons relevées sur les produits existants.</param>
    public static IReadOnlyList<string> Composer(IEnumerable<string> employees)
    {
        var liste = new List<string>(Standard);

        var supplements = employees
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            // La comparaison ignore la casse et les accents : « ete » saisi à
            // la va-vite ne doit pas créer une cinquième saison à côté d'« Été ».
            .Where(s => !liste.Any(connue => Equivalent(connue, s)))
            .Distinct(ComparateurSouple.Instance)
            .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase);

        liste.AddRange(supplements);

        return liste;
    }

    private static bool Equivalent(string gauche, string droite) =>
        string.Compare(gauche, droite, CultureInfo.CurrentCulture,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

    private sealed class ComparateurSouple : IEqualityComparer<string>
    {
        public static readonly ComparateurSouple Instance = new();

        public bool Equals(string? gauche, string? droite) =>
            gauche is null || droite is null
                ? gauche is null && droite is null
                : Equivalent(gauche, droite);

        // Deux textes équivalents doivent partager leur empreinte : celle-ci
        // est prise sur le texte réduit aux lettres sans accent ni casse.
        public int GetHashCode(string valeur) =>
            CultureInfo.CurrentCulture.CompareInfo
                .GetHashCode(valeur, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
    }
}
