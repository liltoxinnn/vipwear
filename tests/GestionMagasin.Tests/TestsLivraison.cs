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

    private static string[] Scripts =>
    [
        Path.Combine(Racine, "publier.ps1"),
        Path.Combine(Racine, "outils", "telecharger-postgres.ps1")
    ];

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
    /// L'icône du programme doit être tirée du logo du magasin, et la
    /// conversion doit précéder la compilation : le fichier projet lit
    /// « logo.ico » au moment où il fabrique l'exécutable. Convertie après,
    /// elle n'aurait servi qu'à la publication suivante — et le magasin
    /// aurait vu son logo dans le logiciel mais l'emblème dessiné sur son
    /// raccourci de bureau.
    /// </summary>
    [Fact]
    public void L_icone_est_tiree_du_logo_avant_la_compilation()
    {
        Assert.Contains("Convertir-LogoEnIcone", Publication, StringComparison.Ordinal);

        var conversion = Publication.IndexOf("Convertir-LogoEnIcone `", StringComparison.Ordinal);
        var compilation = Publication.IndexOf("dotnet publish", StringComparison.Ordinal);

        Assert.True(conversion > 0, "La conversion du logo en icône est absente.");
        Assert.True(compilation > 0, "L'étape de compilation est introuvable.");

        Assert.True(conversion < compilation,
            "Le logo est converti après la compilation : l'exécutable porterait " +
            "encore l'icône de la publication précédente.");
    }

    /// <summary>
    /// Windows PowerShell lit un script sans marque d'ordre des octets dans
    /// la page de codes du système, et non en UTF-8. « échoué » s'y affiche
    /// « Ã©chouÃ© », et le nom de l'enseigne « VIP MENâ€™S STORE ». Les
    /// messages destinés à l'exploitant deviennent illisibles au moment
    /// précis où il en a besoin.
    /// </summary>
    [Fact]
    public void Les_scripts_portent_la_marque_d_ordre_des_octets()
    {
        foreach (var script in Scripts)
        {
            var debut = new byte[3];

            using (var flux = File.OpenRead(script))
            {
                Assert.Equal(3, flux.Read(debut, 0, 3));
            }

            Assert.True(
                debut is [0xEF, 0xBB, 0xBF],
                $"{Path.GetFileName(script)} n'a pas de marque UTF-8 : " +
                "ses accents seront illisibles sous Windows PowerShell.");
        }
    }

    /// <summary>
    /// Le SDK .NET manque sur un poste qui n'a jamais servi à construire.
    /// Sans contrôle, l'échec arrive sous la forme d'un mur d'anglais qui ne
    /// dit ni ce qui manque, ni où le prendre.
    /// </summary>
    [Fact]
    public void La_publication_verifie_les_outils_du_poste()
    {
        var script = Publication;

        Assert.Contains("dotnet --list-sdks", script, StringComparison.Ordinal);
        Assert.Contains("dotnet.microsoft.com/download/dotnet/10.0", script, StringComparison.Ordinal);

        // Le contrôle doit précéder la première compilation, sinon il ne sert
        // à rien : l'erreur brute serait déjà passée.
        Assert.True(
            script.IndexOf("dotnet --list-sdks", StringComparison.Ordinal)
                < script.IndexOf("dotnet publish", StringComparison.Ordinal),
            "Les outils doivent être vérifiés avant la première compilation.");
    }

    /// <summary>
    /// Le séparateur employé dans les noms d'entrées d'une archive dépend de
    /// l'outil qui l'a écrite, jamais de son contenu. Chercher « pgsql/ »
    /// dans une archive qui contient « pgsql\ » a fait refuser un paquet
    /// parfaitement complet, et bloqué une livraison.
    /// </summary>
    [Fact]
    public void La_verification_de_l_archive_admet_les_deux_separateurs()
    {
        var script = Publication;

        // L'archive est écrite avec des séparateurs canoniques…
        Assert.Contains("ZipFile]::CreateFromDirectory", script, StringComparison.Ordinal);

        // …et plus par Compress-Archive, dont le séparateur varie. Le mot
        // subsiste dans un commentaire : seul un appel est fautif.
        var appels = script
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !l.StartsWith('#') && l.Contains("Compress-Archive", StringComparison.Ordinal));

        Assert.Empty(appels);

        // …et relue sans supposer lesquels.
        Assert.Contains("-replace", script, StringComparison.Ordinal);
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
