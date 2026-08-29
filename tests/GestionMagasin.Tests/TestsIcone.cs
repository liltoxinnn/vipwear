using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Icône du programme.
///
/// Sans icône déclarée, Windows affiche celle de WPF par défaut — crénelée —
/// dans chaque barre de titre, dans la barre des tâches et sur le raccourci
/// du bureau. C'est la première chose que voit le magasin, avant même
/// d'ouvrir le logiciel.
/// </summary>
public class TestsIcone
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

    [Fact]
    public void Le_projet_declare_une_icone_de_recours_qui_existe()
    {
        var projet = File.ReadAllText(
            Path.Combine(DossierInterface, "GestionMagasin.App.csproj"));

        var declarations = System.Text.RegularExpressions.Regex.Matches(
            projet, @"<ApplicationIcon[^>]*>([^<]+)</ApplicationIcon>");

        Assert.True(declarations.Count > 0,
            "Aucune icône déclarée : Windows retomberait sur celle de WPF.");

        // « logo.ico » est produit par le script de publication à partir du
        // logo du magasin, et n'est donc pas versionné. L'emblème dessiné,
        // lui, doit toujours être là : c'est le recours.
        var recours = declarations
            .Select(d => d.Groups[1].Value.Trim())
            .Where(nom => !nom.Equals("logo.ico", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(recours.Count > 0,
            "Aucune icône de recours : sans logo de magasin, le programme n'en aurait aucune.");

        foreach (var nom in recours)
        {
            var fichier = Path.Combine(DossierInterface, nom);

            Assert.True(File.Exists(fichier), $"L'icône déclarée est introuvable : {fichier}");
        }
    }

    /// <summary>
    /// Sans conscience du zoom d'affichage, Windows dessine le logiciel à
    /// cent pour cent puis étire l'image entière au facteur voulu. Sur un
    /// écran réglé à 125 ou 150 % — le réglage d'usine de presque tous les
    /// portables — tout devient flou : le texte, les bordures, le logo. Le
    /// magasin croit alors le logiciel mal fait.
    /// </summary>
    [Fact]
    public void Le_manifeste_declare_la_conscience_du_zoom_d_affichage()
    {
        var projet = File.ReadAllText(
            Path.Combine(DossierInterface, "GestionMagasin.App.csproj"));

        var declaration = System.Text.RegularExpressions.Regex.Match(
            projet, @"<ApplicationManifest>([^<]+)</ApplicationManifest>");

        Assert.True(declaration.Success,
            "Aucun manifeste déclaré : le logiciel serait étiré au lieu d'être redessiné.");

        var fichier = Path.Combine(DossierInterface, declaration.Groups[1].Value.Trim());

        Assert.True(File.Exists(fichier), $"Le manifeste est introuvable : {fichier}");

        var manifeste = File.ReadAllText(fichier);

        Assert.Contains("PerMonitorV2", manifeste, StringComparison.Ordinal);
        Assert.Contains("<dpiAware", manifeste, StringComparison.Ordinal);

        // La conscience par moniteur n'est honorée que si le programme se
        // déclare compatible avec Windows 10 : sans cette ligne, Windows le
        // traite comme écrit pour Vista et l'étire quand même.
        Assert.Contains("8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a", manifeste, StringComparison.Ordinal);
    }

    /// <summary>
    /// Windows choisit dans le fichier la taille qui lui convient. Une icône
    /// ne portant que le grand format serait réduite à la volée dans la barre
    /// des tâches, et y paraîtrait floue.
    /// </summary>
    [Fact]
    public void L_icone_porte_les_petites_tailles_comme_les_grandes()
    {
        var fichier = Path.Combine(DossierInterface, "vipmensstore.ico");
        var octets = File.ReadAllBytes(fichier);

        // En-tête ICO : deux octets nuls, le type 1, puis le nombre d'images.
        Assert.Equal(0, octets[0]);
        Assert.Equal(0, octets[1]);
        Assert.Equal(1, octets[2]);

        var nombre = octets[4] | (octets[5] << 8);

        Assert.True(nombre >= 5,
            $"L'icône ne contient que {nombre} taille(s) : Windows en réduirait une à la volée.");

        var tailles = new List<int>();

        for (var i = 0; i < nombre; i++)
        {
            // Chaque entrée fait seize octets ; la largeur est le premier,
            // et zéro y désigne 256.
            var largeur = octets[6 + i * 16];

            tailles.Add(largeur == 0 ? 256 : largeur);
        }

        Assert.Contains(16, tailles);
        Assert.Contains(32, tailles);
        Assert.Contains(256, tailles);
    }
}
