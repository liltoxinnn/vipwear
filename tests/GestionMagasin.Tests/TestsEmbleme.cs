using System.Text.RegularExpressions;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Contrôles sur l'emblème de l'enseigne.
///
/// Le blason n'est pas une image : c'est du tracé vectoriel construit à
/// l'exécution. Un tracé mal formé ne se voit ni à la compilation ni dans les
/// autres écrans — il fait échouer l'écran de connexion, donc le logiciel
/// entier, et seulement chez le client.
/// </summary>
public class TestsEmbleme
{
    private static string FichierBlason =>
        Path.Combine(DossierInterface, "Views", "Blason.cs");

    private static string DossierInterface
    {
        get
        {
            var dossier = new DirectoryInfo(AppContext.BaseDirectory);

            while (dossier is not null)
            {
                var candidat = Path.Combine(dossier.FullName, "src", "GestionMagasin.App");

                if (Directory.Exists(candidat))
                {
                    return candidat;
                }

                dossier = dossier.Parent;
            }

            throw new DirectoryNotFoundException(
                "Le projet d'interface est introuvable depuis " + AppContext.BaseDirectory);
        }
    }

    private static IEnumerable<(string Nom, string Trace)> Traces()
    {
        var source = File.ReadAllText(FichierBlason);

        foreach (Match declaration in Regex.Matches(
                     source, @"const string (\w+)\s*=\s*(.*?);", RegexOptions.Singleline))
        {
            var morceaux = Regex.Matches(declaration.Groups[2].Value, @"""([^""]*)""")
                .Select(m => m.Groups[1].Value);

            yield return (declaration.Groups[1].Value, string.Concat(morceaux));
        }
    }

    /// <summary>
    /// Un tracé n'accepte que ses propres lettres de commande. Un caractère
    /// étranger — glissé par une retouche — lève une exception au moment où
    /// l'écran s'affiche, pas avant.
    /// </summary>
    [Fact]
    public void Les_traces_du_blason_sont_du_langage_de_chemin_valide()
    {
        var traces = Traces().ToList();

        Assert.NotEmpty(traces);

        foreach (var (nom, trace) in traces)
        {
            var corps = Regex.Replace(trace, @"^F[01]\s+", string.Empty);

            Assert.True(corps.StartsWith('M'), $"{nom} ne commence pas par un déplacement.");

            var etranger = Regex.Match(corps, @"[^MLHVCQSTAZmlhvcqstaz0-9\.,\-\s]");

            Assert.False(etranger.Success,
                $"{nom} contient « {etranger.Value} », qui n'appartient pas au langage de chemin.");

            Assert.True(
                corps.Count(c => c is 'Z' or 'z') > 0,
                $"{nom} ne referme aucune de ses figures : la surface remplie serait imprévisible.");
        }
    }

    /// <summary>
    /// Les mèches et les traits du visage se recouvrent. La règle de
    /// remplissage pair-impair y percerait des trous : chaque tracé doit
    /// donc annoncer la règle non nulle.
    /// </summary>
    [Fact]
    public void Chaque_trace_annonce_sa_regle_de_remplissage()
    {
        foreach (var (nom, trace) in Traces())
        {
            Assert.True(trace.StartsWith("F1 ", StringComparison.Ordinal),
                $"{nom} ne précise pas la règle de remplissage non nulle.");
        }
    }

    /// <summary>
    /// Le logiciel tourne en français : « 57.25 » s'y écrit « 57,25 », et une
    /// virgule décimale au milieu d'un tracé le rend illisible. Toute mise en
    /// forme de coordonnée doit donc imposer la culture invariante.
    /// </summary>
    [Fact]
    public void Aucune_coordonnee_n_est_mise_en_forme_selon_la_culture_locale()
    {
        var lignes = File.ReadAllLines(FichierBlason);
        var fautes = new List<string>();

        for (var i = 0; i < lignes.Length; i++)
        {
            if (!Regex.IsMatch(lignes[i], @"\{[A-Za-z_][^{}]*:0\.#"))
            {
                continue;
            }

            // La culture peut être annoncée sur l'appel, quelques lignes plus haut.
            var voisinage = string.Join(
                ' ', lignes.Skip(Math.Max(0, i - 4)).Take(Math.Min(5, i + 1)));

            if (!voisinage.Contains("InvariantCulture", StringComparison.Ordinal))
            {
                fautes.Add($"Blason.cs:{i + 1} — {lignes[i].Trim()}");
            }
        }

        Assert.True(fautes.Count == 0,
            "Coordonnées mises en forme sans culture invariante :" + Environment.NewLine +
            string.Join(Environment.NewLine, fautes));
    }

    /// <summary>
    /// Le nom de l'enseigne figure sur les deux fenêtres. Un logiciel livré
    /// sous un autre nom que celui de la devanture décrédibilise le reste.
    /// </summary>
    [Fact]
    public void Les_fenetres_portent_le_nom_de_l_enseigne()
    {
        foreach (var nom in new[] { "FenetreConnexion.xaml", "FenetrePrincipale.xaml" })
        {
            var contenu = File.ReadAllText(Path.Combine(DossierInterface, "Views", nom));
            var titre = Regex.Match(contenu, @"Title=""([^""]*)""");

            Assert.True(titre.Success, $"{nom} n'a pas de titre.");

            Assert.Contains("VIP MEN’S STORE", titre.Groups[1].Value, StringComparison.Ordinal);
        }
    }
}
