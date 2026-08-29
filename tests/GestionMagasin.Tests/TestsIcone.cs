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
    public void Le_projet_declare_une_icone_qui_existe()
    {
        var projet = File.ReadAllText(
            Path.Combine(DossierInterface, "GestionMagasin.App.csproj"));

        var declaration = System.Text.RegularExpressions.Regex.Match(
            projet, @"<ApplicationIcon>([^<]+)</ApplicationIcon>");

        Assert.True(declaration.Success,
            "Aucune icône déclarée : Windows retomberait sur celle de WPF.");

        var fichier = Path.Combine(DossierInterface, declaration.Groups[1].Value.Trim());

        Assert.True(File.Exists(fichier),
            $"L'icône déclarée est introuvable : {fichier}");
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
