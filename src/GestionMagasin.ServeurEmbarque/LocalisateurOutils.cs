using System.Runtime.InteropServices;

namespace GestionMagasin.ServeurEmbarque;

/// <summary>
/// Retrouve les exécutables de PostgreSQL (initdb, pg_ctl, pg_dump…).
///
/// Deux cas sont prévus. Sur le poste d'un magasin, les binaires sont livrés
/// dans le dossier du logiciel : rien n'est installé sur la machine. Sur un
/// poste de développement, ceux du système sont utilisés, ce qui permet
/// d'exécuter les tests sans embarquer 300 Mo dans le dépôt.
/// </summary>
public static class LocalisateurOutils
{
    /// <summary>Nom du dossier livré à côté de l'application.</summary>
    public const string NomDossierEmbarque = "pgsql";

    private static string Executable(string nom) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? nom + ".exe" : nom;

    /// <summary>
    /// Dossier « bin » de PostgreSQL, ou null si aucun n'a été trouvé.
    /// Le dossier embarqué a la priorité : s'il est présent, c'est lui qui
    /// doit servir, même si la machine a par ailleurs son propre PostgreSQL.
    /// </summary>
    public static string? TrouverDossierBin(string? racineEmbarquee = null)
    {
        foreach (var candidat in CandidatsEmbarques(racineEmbarquee))
        {
            if (File.Exists(Path.Combine(candidat, Executable("pg_ctl"))))
            {
                return candidat;
            }
        }

        return TrouverDansLeSysteme();
    }

    /// <summary>Vrai lorsque les binaires sont livrés avec l'application.</summary>
    public static bool ServeurEmbarquePresent(string? racineEmbarquee = null) =>
        CandidatsEmbarques(racineEmbarquee)
            .Any(c => File.Exists(Path.Combine(c, Executable("pg_ctl"))));

    /// <summary>Chemin complet d'un outil PostgreSQL.</summary>
    public static string Outil(string dossierBin, string nom) =>
        Path.Combine(dossierBin, Executable(nom));

    private static IEnumerable<string> CandidatsEmbarques(string? racineEmbarquee)
    {
        var racine = racineEmbarquee ?? AppContext.BaseDirectory;

        yield return Path.Combine(racine, NomDossierEmbarque, "bin");
        yield return Path.Combine(racine, "bin");
    }

    /// <summary>
    /// PostgreSQL installé sur la machine. Sous Linux les binaires ne sont pas
    /// dans le PATH mais rangés par version, d'où la recherche explicite.
    /// </summary>
    private static string? TrouverDansLeSysteme()
    {
        foreach (var dossier in DossiersSysteme())
        {
            if (Directory.Exists(dossier)
                && File.Exists(Path.Combine(dossier, Executable("pg_ctl"))))
            {
                return dossier;
            }
        }

        return null;
    }

    private static IEnumerable<string> DossiersSysteme()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const string baseWindows = @"C:\Program Files\PostgreSQL";

            if (Directory.Exists(baseWindows))
            {
                // La version la plus récente d'abord.
                foreach (var version in Directory.GetDirectories(baseWindows)
                             .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    yield return Path.Combine(version, "bin");
                }
            }

            yield break;
        }

        const string baseLinux = "/usr/lib/postgresql";

        if (Directory.Exists(baseLinux))
        {
            foreach (var version in Directory.GetDirectories(baseLinux)
                         .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
            {
                yield return Path.Combine(version, "bin");
            }
        }

        yield return "/usr/local/pgsql/bin";
        yield return "/opt/homebrew/bin";
        yield return "/usr/bin";
    }
}
