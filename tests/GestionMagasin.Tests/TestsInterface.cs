using System.Text.RegularExpressions;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Contrôles sur les fichiers d'interface.
///
/// Le compilateur ne vérifie pas le contenu des liaisons XAML : une ressource
/// absente ou du mauvais type ne se manifeste qu'à l'exécution, souvent chez
/// le client et sur un seul écran. Chacune des règles ci-dessous correspond à
/// une panne réellement survenue.
/// </summary>
public class TestsInterface
{
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

    private static IEnumerable<string> FichiersXaml() =>
        Directory.EnumerateFiles(DossierInterface, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    /// <summary>
    /// Une couleur n'est pas un pinceau. Employer l'une pour l'autre lève
    /// « n'est pas une valeur valide pour la propriété Stroke » au moment
    /// précis où l'écran s'affiche.
    /// </summary>
    [Fact]
    public void Aucune_couleur_n_est_employee_la_ou_un_pinceau_est_attendu()
    {
        var fautes = new List<string>();

        foreach (var fichier in FichiersXaml())
        {
            var lignes = File.ReadAllLines(fichier);

            for (var i = 0; i < lignes.Length; i++)
            {
                foreach (Match correspondance in
                         Regex.Matches(lignes[i], @"(\w+)\s*=\s*""\{StaticResource\s+(Couleur\w+)\s*\}"""))
                {
                    // « Color="{StaticResource CouleurX}" » est le seul usage correct.
                    if (correspondance.Groups[1].Value != "Color")
                    {
                        fautes.Add($"{Path.GetFileName(fichier)}:{i + 1} — " +
                                   $"{correspondance.Groups[1].Value} reçoit {correspondance.Groups[2].Value}");
                    }
                }

                foreach (Match correspondance in
                         Regex.Matches(lignes[i], @"\bColor\s*=\s*""\{StaticResource\s+(Pinceau\w+)\s*\}"""))
                {
                    fautes.Add($"{Path.GetFileName(fichier)}:{i + 1} — " +
                               $"Color reçoit le pinceau {correspondance.Groups[1].Value}");
                }
            }
        }

        Assert.True(fautes.Count == 0,
            "Couleur et pinceau confondus :" + Environment.NewLine + string.Join(Environment.NewLine, fautes));
    }

    /// <summary>
    /// Une ressource nommée mais jamais définie fait échouer l'écran qui
    /// l'emploie, et lui seul.
    /// </summary>
    [Fact]
    public void Toutes_les_ressources_nommees_sont_definies()
    {
        var definies = new HashSet<string>();
        var employees = new Dictionary<string, string>();

        foreach (var fichier in FichiersXaml())
        {
            var contenu = File.ReadAllText(fichier);

            foreach (Match correspondance in Regex.Matches(contenu, @"x:Key=""([^""]+)"""))
            {
                definies.Add(correspondance.Groups[1].Value);
            }

            foreach (Match correspondance in
                     Regex.Matches(contenu, @"\{StaticResource\s+([A-Za-z0-9_]+)\s*\}"))
            {
                employees.TryAdd(correspondance.Groups[1].Value, Path.GetFileName(fichier));
            }
        }

        var absentes = employees.Where(e => !definies.Contains(e.Key)).ToList();

        Assert.True(absentes.Count == 0,
            "Ressources employées mais jamais définies :" + Environment.NewLine +
            string.Join(Environment.NewLine, absentes.Select(a => $"  {a.Key} (vue dans {a.Value})")));
    }

    /// <summary>
    /// Un dictionnaire fusionné ne voit que ceux fusionnés avant lui. Cette
    /// règle avait empêché l'ouverture de cinq écrans, dont la caisse.
    /// </summary>
    [Fact]
    public void Chaque_dictionnaire_ne_depend_que_de_ceux_fusionnes_avant_lui()
    {
        var dossier = Path.Combine(DossierInterface, "Resources");

        var ordre = Regex.Matches(
                File.ReadAllText(Path.Combine(DossierInterface, "App.xaml")),
                @"<ResourceDictionary\s+Source=""Resources/([^""]+)""\s*/>")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(ordre);

        var visibles = new HashSet<string>();
        var fautes = new List<string>();

        foreach (var nom in ordre)
        {
            var contenu = File.ReadAllText(Path.Combine(dossier, nom));

            foreach (Match correspondance in Regex.Matches(contenu, @"x:Key=""([^""]+)"""))
            {
                visibles.Add(correspondance.Groups[1].Value);
            }

            foreach (Match correspondance in
                     Regex.Matches(contenu, @"\{StaticResource\s+([A-Za-z0-9_]+)\s*\}"))
            {
                if (!visibles.Contains(correspondance.Groups[1].Value))
                {
                    fautes.Add($"{nom} emploie « {correspondance.Groups[1].Value} », " +
                               "défini plus tard ou ailleurs");
                }
            }
        }

        Assert.True(fautes.Count == 0,
            "Ordre de fusion non respecté :" + Environment.NewLine + string.Join(Environment.NewLine, fautes));
    }

    /// <summary>Deux ressources de même nom : celle qui l'emporte est imprévisible.</summary>
    [Fact]
    public void Aucune_ressource_n_est_definie_deux_fois()
    {
        var comptes = new Dictionary<string, List<string>>();

        foreach (var fichier in FichiersXaml())
        {
            foreach (Match correspondance in
                     Regex.Matches(File.ReadAllText(fichier), @"x:Key=""([^""]+)"""))
            {
                var cle = correspondance.Groups[1].Value;

                if (!comptes.TryGetValue(cle, out var fichiers))
                {
                    comptes[cle] = fichiers = [];
                }

                fichiers.Add(Path.GetFileName(fichier));
            }
        }

        var doublons = comptes.Where(c => c.Value.Count > 1).ToList();

        Assert.True(doublons.Count == 0,
            "Ressources définies plusieurs fois :" + Environment.NewLine +
            string.Join(Environment.NewLine, doublons.Select(d => $"  {d.Key} : {string.Join(", ", d.Value)}")));
    }
}
