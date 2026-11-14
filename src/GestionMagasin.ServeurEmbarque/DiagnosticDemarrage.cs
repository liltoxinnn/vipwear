namespace GestionMagasin.ServeurEmbarque;

/// <summary>
/// Explique en français pourquoi le serveur de base de données n'a pas
/// démarré.
///
/// Deux sources sont lues : le journal du serveur, mais seulement ce que
/// cette tentative-ci y a écrit, et ce que « pg_ctl » a affiché. Le journal
/// garde les lignes de toutes les sessions passées : y prendre la dernière
/// erreur venue montrait au magasin une ligne vieille de plusieurs jours —
/// « terminating connection due to administrator command », qui n'est que
/// la trace d'un arrêt normal — à la place de la vraie raison.
/// </summary>
public static class DiagnosticDemarrage
{
    /// <summary>
    /// Causes reconnues, examinées dans l'ordre, de la plus précise à la plus
    /// générale : « Permission denied » apparaît aussi bien pour un port
    /// refusé que pour un dossier interdit en écriture, et seule la phrase
    /// complète les distingue.
    /// </summary>
    private static readonly (string Motif, string Explication)[] CausesConnues =
    [
        // Un disque plein empêche aussi d'écrire le verrou : « could not
        // write lock file: No space left on device » doit parler du disque,
        // pas d'une session mal fermée.
        ("no space left", "le disque est plein"),
        ("could not create any TCP/IP sockets",
            "Windows a refusé au logiciel l'ouverture de son port réseau local"),
        ("could not bind", "le port de la base de données n'a pas pu être ouvert"),
        ("address already in use", "le port de la base de données est déjà occupé par un autre programme"),
        ("shared memory block is still in use",
            "un morceau de la session précédente tourne encore en arrière-plan ; redémarrer l'ordinateur l'arrête"),
        ("lock file", "une session précédente ne s'est pas refermée correctement"),
        ("incompatible", "les fichiers de données proviennent d'une autre version de PostgreSQL"),
        ("invalid permissions", "le dossier de données a des droits que PostgreSQL refuse"),
        ("did not start in time",
            "le serveur a mis trop de temps à démarrer : l'ordinateur était très occupé, ou l'antivirus " +
            "analysait le logiciel"),
        ("being used by another process",
            "un fichier de la base de données est retenu par un autre programme (souvent l'antivirus)"),
        ("utilisé par un autre processus",
            "un fichier de la base de données est retenu par un autre programme (souvent l'antivirus)"),
        ("could not open file", "un fichier de la base de données n'a pas pu être lu"),
        ("permission denied", "le logiciel n'a pas le droit d'écrire dans son dossier de données"),
        ("access is denied", "Windows a refusé l'accès à un fichier de la base de données (souvent l'antivirus)"),
        ("accès refusé", "Windows a refusé l'accès à un fichier de la base de données (souvent l'antivirus)"),
        ("administrative permissions",
            "le logiciel a été lancé en tant qu'administrateur, ce que la base de données refuse"),
        ("data directory", "le dossier de données est inutilisable")
    ];

    /// <summary>Raison de l'échec, ou rien si aucune source n'en dit quoi que ce soit.</summary>
    /// <param name="journalDeLaTentative">Lignes écrites au journal du serveur par cette tentative seulement.</param>
    /// <param name="sortiePgCtl">Ce que « pg_ctl start » a affiché.</param>
    public static string? Expliquer(string? journalDeLaTentative, string? sortiePgCtl)
    {
        // Le journal du serveur est le plus précis : pg_ctl se contente le
        // plus souvent de « could not start server ».
        foreach (var source in new[] { journalDeLaTentative, sortiePgCtl })
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            foreach (var (motif, explication) in CausesConnues)
            {
                if (source.Contains(motif, StringComparison.OrdinalIgnoreCase))
                {
                    return explication + ".";
                }
            }
        }

        // Cause inconnue : la dernière erreur grave de cette tentative, en
        // anglais, vaut mieux que rien ; à défaut, le dernier mot de pg_ctl.
        return DernieresLignes(journalDeLaTentative)
                   .LastOrDefault(l => l.Contains("FATAL", StringComparison.OrdinalIgnoreCase)
                                       || l.Contains("PANIC", StringComparison.OrdinalIgnoreCase))
               ?? DernieresLignes(sortiePgCtl)
                   .LastOrDefault(l => l.StartsWith("pg_ctl:", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> DernieresLignes(string? texte) =>
        string.IsNullOrWhiteSpace(texte)
            ? []
            : texte.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
