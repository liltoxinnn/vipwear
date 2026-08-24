using System.Text.RegularExpressions;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Contrôles sur le paquet livré au magasin.
///
/// Le poste du magasin n'installe rien : le logiciel apporte sa base de
/// données. Un paquet auquel elle manque ne se découvre que chez le client,
/// devant un message qu'il ne peut pas interpréter. Ces règles ferment les
/// portes par lesquelles un paquet incomplet est déjà sorti.
/// </summary>
public class TestsLivraison
{
    private static string Racine
    {
        get
        {
            var dossier = new DirectoryInfo(AppContext.BaseDirectory);

            while (dossier is not null)
            {
                if (File.Exists(Path.Combine(dossier.FullName, "publier.ps1")))
                {
                    return dossier.FullName;
                }

                dossier = dossier.Parent;
            }

            throw new FileNotFoundException(
                "Le script de publication est introuvable depuis " + AppContext.BaseDirectory);
        }
    }

    private static string Publication => File.ReadAllText(Path.Combine(Racine, "publier.ps1"));

    private static string Demarrage => File.ReadAllText(
        Path.Combine(Racine, "src", "GestionMagasin.App", "App.xaml.cs"));

    /// <summary>
    /// Le marqueur écrit par le script et celui cherché par le logiciel
    /// doivent porter le même nom. Renommer l'un sans l'autre rendrait le
    /// contrôle muet, sans que rien n'échoue.
    /// </summary>
    [Fact]
    public void Le_marqueur_de_livraison_porte_le_meme_nom_des_deux_cotes()
    {
        // Le nom fait foi côté logiciel : c'est lui qui décide du message
        // affiché au magasin. Le script doit écrire ce fichier-là.
        var lu = Regex.Match(Demarrage, @"AppContext\.BaseDirectory, ""([\w.]+\.txt)""");

        Assert.True(lu.Success, "Le logiciel ne cherche aucun marqueur de livraison.");

        var marqueur = lu.Groups[1].Value;

        Assert.Contains(
            $@"Join-Path $cheminSortie ""{marqueur}""",
            Publication,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Vérifier la source ne suffit pas : c'est le dossier produit qui part
    /// chez le client. Une copie interrompue ou un antivirus trop zélé le
    /// vide sans que rien ne le signale.
    /// </summary>
    [Fact]
    public void La_publication_verifie_le_paquet_produit()
    {
        string[] indispensables =
        [
            "GestionMagasin.exe",
            @"pgsql\bin\pg_ctl.exe",
            @"pgsql\bin\postgres.exe",
            @"pgsql\bin\initdb.exe",
            @"pgsql\bin\pg_dump.exe",
            @"pgsql\bin\pg_restore.exe"
        ];

        var script = Publication;

        foreach (var fichier in indispensables)
        {
            Assert.Contains(fichier, script, StringComparison.Ordinal);
        }

        // Le dossier produit, et pas seulement la source.
        Assert.Contains("Join-Path $cheminSortie $fichier", script, StringComparison.Ordinal);

        // Et l'archive, qui est ce qui part réellement.
        Assert.Contains("ZipFile]::OpenRead", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Le dossier « pgsql » n'est pas dans le dépôt. Sur un poste fraîchement
    /// cloné, oublier de le récupérer produisait un paquet sans base de
    /// données. Le script le récupère donc lui-même.
    /// </summary>
    [Fact]
    public void La_publication_recupere_postgresql_si_besoin()
    {
        Assert.Contains("telecharger-postgres.ps1", Publication, StringComparison.Ordinal);
    }

    /// <summary>
    /// Une publication à laquelle il manque un fichier doit s'arrêter, pas
    /// se contenter d'un avertissement que personne ne lit.
    /// </summary>
    [Fact]
    public void Un_paquet_incomplet_interrompt_la_publication()
    {
        var script = Publication;

        Assert.Contains("PAQUET INCOMPLET", script, StringComparison.Ordinal);
        Assert.Contains("L'ARCHIVE EST INCOMPLETE", script, StringComparison.Ordinal);

        // Deux arrêts, et non deux messages.
        Assert.True(
            Regex.Matches(script, @"exit 1").Count >= 3,
            "La publication doit s'interrompre lorsqu'elle produit un paquet incomplet.");
    }
}
