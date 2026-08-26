using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Npgsql;

namespace GestionMagasin.ServeurEmbarque;

/// <summary>Erreur empêchant le serveur embarqué de fonctionner.</summary>
public sealed class ServeurEmbarqueException : Exception
{
    public ServeurEmbarqueException(string message, Exception? interne = null)
        : base(message, interne) { }

    /// <summary>
    /// Sortie brute des programmes PostgreSQL, en anglais.
    ///
    /// Elle est destinée au journal technique, jamais à l'utilisateur du
    /// magasin : le message, lui, est rédigé en français et indique quoi
    /// faire.
    /// </summary>
    public string? DetailTechnique { get; init; }
}

/// <summary>
/// Serveur PostgreSQL démarré par l'application elle-même, à partir de
/// binaires livrés dans son dossier.
///
/// Le magasin n'installe rien et ne choisit aucun mot de passe : celui-ci est
/// tiré au hasard au premier démarrage et conservé dans le dossier de
/// données. Le serveur n'écoute que sur 127.0.0.1, il n'est donc joignable
/// depuis aucune autre machine du réseau.
/// </summary>
public sealed class ServeurPostgresEmbarque : IAsyncDisposable
{
    private readonly OptionsServeur _options;
    private readonly string _dossierBin;
    private readonly Action<string>? _journaliser;

    /// <summary>
    /// Vrai lorsque l'arrêt du serveur nous incombe.
    ///
    /// C'est le cas dès que la grappe nous appartient, qu'elle ait été
    /// démarrée par ce processus ou laissée en marche par une session
    /// précédente mal fermée. Sans cela, un plantage laisserait PostgreSQL
    /// tourner indéfiniment, port et fichiers occupés.
    /// </summary>
    private bool _aNotreCharge;

    public ServeurPostgresEmbarque(OptionsServeur? options = null, Action<string>? journaliser = null)
    {
        _options = options ?? new OptionsServeur();
        _journaliser = journaliser;

        _dossierBin = LocalisateurOutils.TrouverDossierBin(_options.RacineBinaires)
            ?? throw new ServeurEmbarqueException(
                "Les fichiers de PostgreSQL sont introuvables. Le dossier « pgsql » " +
                "doit se trouver à côté du programme.");

        UtiliseServeurLivre = LocalisateurOutils.ServeurEmbarquePresent(_options.RacineBinaires);
    }

    /// <summary>Dossier des programmes PostgreSQL réellement employés.</summary>
    public string DossierOutils => _dossierBin;

    /// <summary>
    /// Vrai lorsque les programmes utilisés sont ceux livrés avec
    /// l'application, faux lorsqu'ils proviennent d'un PostgreSQL installé
    /// sur la machine. Sur le poste d'un magasin, seul le premier cas doit se
    /// présenter : il faut pouvoir le vérifier.
    /// </summary>
    public bool UtiliseServeurLivre { get; private set; }

    /// <summary>Fichier conservant le mot de passe tiré au premier démarrage.</summary>
    private string CheminMotDePasse => Path.Combine(_options.DossierDonnees, "..", "acces.txt");

    /// <summary>Marqueur écrit une fois la base applicative créée.</summary>
    private string CheminMarqueurBase => Path.Combine(_options.DossierDonnees, "..", "base-creee.txt");

    /// <summary>Chaîne de connexion vers la base du magasin. Valide après démarrage.</summary>
    public string ChaineConnexion { get; private set; } = string.Empty;

    // ==================================================================
    // Démarrage
    // ==================================================================

    /// <summary>
    /// Prépare le serveur si nécessaire, le démarre, et retourne la chaîne de
    /// connexion à utiliser. L'opération est sans effet si le serveur tourne
    /// déjà : c'est le cas après un arrêt brutal de l'application.
    /// </summary>
    /// <summary>
    /// Port réellement retenu au démarrage. Il peut différer du port souhaité
    /// lorsque celui-ci est interdit par le système.
    /// </summary>
    public int PortEffectif { get; private set; }

    public async Task<string> DemarrerAsync(CancellationToken jeton = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(CheminMotDePasse))!);

        PortEffectif = ChoisirPort();

        var motDePasse = ObtenirOuCreerMotDePasse();

        if (!ClusterInitialise())
        {
            Journaliser("Première utilisation : préparation de la base de données.");
            await InitialiserClusterAsync(motDePasse, jeton).ConfigureAwait(false);
        }
        else
        {
            await VerifierCompatibiliteAsync(jeton).ConfigureAwait(false);
        }

        SupprimerVerrouPerime();

        if (await GrappeEnMarcheAsync(jeton).ConfigureAwait(false))
        {
            // Reste d'une exécution précédente mal fermée. Le dossier de
            // données n'appartient qu'à ce logiciel : reprendre ce serveur
            // est sûr, et c'est le seul moyen qu'il finisse par s'arrêter.
            //
            // Il écoute le port qu'on lui avait donné, pas celui qui vient
            // d'être retenu : le sien fait foi, sinon l'application irait
            // frapper à une porte fermée.
            if (PortInscritAuVerrou() is { } portRepris && portRepris != PortEffectif)
            {
                Journaliser($"Le serveur repris écoute sur le port {portRepris}.");
                PortEffectif = portRepris;
            }

            Journaliser("Le serveur d'une session précédente était encore en marche : il est repris.");
        }
        else
        {
            await LancerAsync(jeton).ConfigureAwait(false);
        }

        _aNotreCharge = true;

        await AttendreDisponibiliteAsync(jeton).ConfigureAwait(false);
        await GarantirBaseAsync(motDePasse, jeton).ConfigureAwait(false);

        ChaineConnexion = Chaine(_options.NomBase, motDePasse);

        return ChaineConnexion;
    }

    /// <summary>
    /// Arrête le serveur dont ce processus a la charge.
    /// </summary>
    /// <returns>Vrai si un serveur a effectivement été arrêté.</returns>
    public async Task<bool> ArreterAsync(CancellationToken jeton = default)
    {
        if (!_aNotreCharge)
        {
            return false;
        }

        // Les connexions gardées en réserve par l'application doivent être
        // abandonnées avant l'arrêt : sans cela, une reconnexion ultérieure
        // récupérerait une connexion vers un serveur éteint.
        NpgsqlConnection.ClearAllPools();

        // « fast » ferme les connexions en cours puis écrit proprement sur le
        // disque. Aucune écriture validée n'est perdue.
        var resultat = await ExecuterAsync(
            LocalisateurOutils.Outil(_dossierBin, "pg_ctl"),
            ["-D", _options.DossierDonnees, "-m", "fast", "-w", "stop"],
            jeton).ConfigureAwait(false);

        _aNotreCharge = false;

        if (resultat.CodeSortie != 0)
        {
            Journaliser($"Arrêt du serveur en échec : {resultat.Sortie}");

            return false;
        }

        return true;
    }

    /// <summary>
    /// Retire le fichier de verrou laissé par un arrêt brutal.
    ///
    /// PostgreSQL écrit « postmaster.pid » au démarrage et l'efface en
    /// s'arrêtant. Après une coupure de courant ou un processus tué, il reste
    /// en place et le serveur refuse de démarrer.
    ///
    /// Le verrou n'est retiré que si le serveur qu'il désigne n'existe plus.
    /// Constater qu'un processus porte ce numéro ne suffit pas : Windows
    /// réattribue les numéros, et après un redémarrage ou un « taskkill » le
    /// numéro d'un serveur mort désigne souvent un navigateur ou un jeu. Le
    /// verrou paraissait alors valide, et le magasin restait bloqué sur
    /// « Le serveur de base de données n'a pas pu démarrer » jusqu'à ce que
    /// quelqu'un aille effacer le fichier à la main.
    ///
    /// Le nom du processus ET le dossier de données inscrit dans le verrou
    /// sont donc vérifiés : ils ne coïncident que pour notre propre serveur.
    /// </summary>
    private void SupprimerVerrouPerime()
    {
        var verrou = Path.Combine(_options.DossierDonnees, "postmaster.pid");

        if (!File.Exists(verrou))
        {
            return;
        }

        try
        {
            var lignes = File.ReadAllLines(verrou);

            if (!VerrouGrappe.EstPerime(lignes, _options.DossierDonnees, NomDuProcessus))
            {
                return;
            }

            File.Delete(verrou);
            Journaliser("Verrou d'un arrêt précédent retiré : plus aucun serveur ne le tient.");
        }
        catch (Exception erreur) when (erreur is IOException or UnauthorizedAccessException)
        {
            // Le serveur signalera lui-même le problème, avec plus de détail.
        }
    }

    /// <summary>
    /// Port inscrit dans « postmaster.pid » par le serveur en marche.
    /// La quatrième ligne du fichier le porte.
    /// </summary>
    private int? PortInscritAuVerrou()
    {
        try
        {
            var verrou = Path.Combine(_options.DossierDonnees, "postmaster.pid");

            if (!File.Exists(verrou))
            {
                return null;
            }

            var lignes = File.ReadAllLines(verrou);

            return lignes.Length > 3 && int.TryParse(lignes[3].Trim(), out var port) && port > 0
                ? port
                : null;
        }
        catch (Exception erreur) when (erreur is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Nom du processus portant ce numéro, ou null s'il n'existe plus.</summary>
    private static string? NomDuProcessus(int identifiant)
    {
        try
        {
            using var processus = Process.GetProcessById(identifiant);

            return processus.HasExited ? null : processus.ProcessName;
        }
        catch (Exception erreur) when (erreur is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Interroge PostgreSQL sur l'état de NOTRE grappe, désignée par son
    /// dossier de données.
    ///
    /// C'est plus sûr que de tenter une connexion sur le port : un autre
    /// programme pourrait l'occuper, et nous arrêterions alors un serveur qui
    /// ne nous appartient pas.
    /// </summary>
    private async Task<bool> GrappeEnMarcheAsync(CancellationToken jeton)
    {
        var resultat = await ExecuterAsync(
            LocalisateurOutils.Outil(_dossierBin, "pg_ctl"),
            ["-D", _options.DossierDonnees, "status"],
            jeton).ConfigureAwait(false);

        // pg_ctl status : 0 en marche, 3 arrêté, 4 dossier inutilisable.
        return resultat.CodeSortie == 0;
    }

    public async ValueTask DisposeAsync() => await ArreterAsync().ConfigureAwait(false);

    // ==================================================================
    // Préparation
    // ==================================================================

    private bool ClusterInitialise() =>
        File.Exists(Path.Combine(_options.DossierDonnees, "PG_VERSION"));

    /// <summary>
    /// Vérifie que les données existantes ont été créées par la même version
    /// majeure de PostgreSQL que les programmes livrés.
    ///
    /// Une version différente refuserait de démarrer avec un message
    /// technique en anglais. Mieux vaut le dire clairement, et indiquer la
    /// seule marche à suivre : restaurer une sauvegarde.
    /// </summary>
    private async Task VerifierCompatibiliteAsync(CancellationToken jeton)
    {
        var fichierVersion = Path.Combine(_options.DossierDonnees, "PG_VERSION");
        var versionDonnees = (await File.ReadAllTextAsync(fichierVersion, jeton).ConfigureAwait(false)).Trim();

        var resultat = await ExecuterAsync(
            LocalisateurOutils.Outil(_dossierBin, "postgres"),
            ["--version"],
            jeton).ConfigureAwait(false);

        // « postgres (PostgreSQL) 17.2 » : seule la version majeure compte.
        var majeure = resultat.Sortie
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Split('.').FirstOrDefault();

        if (string.IsNullOrEmpty(majeure) || majeure == versionDonnees)
        {
            return;
        }

        throw new ServeurEmbarqueException(
            $"Les données présentes ont été créées avec PostgreSQL {versionDonnees}, " +
            $"alors que cette version du logiciel utilise PostgreSQL {majeure}." +
            Environment.NewLine +
            $"Dossier concerné : « {_options.DossierDonnees} »." + Environment.NewLine +
            "Restaurez une sauvegarde après avoir mis ce dossier de côté.");
    }

    private async Task InitialiserClusterAsync(string motDePasse, CancellationToken jeton)
    {
        Directory.CreateDirectory(_options.DossierDonnees);

        // Le mot de passe passe par un fichier temporaire : il n'apparaît
        // ainsi jamais dans la ligne de commande, visible par les autres
        // processus de la machine.
        var fichierMotDePasse = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await File.WriteAllTextAsync(fichierMotDePasse, motDePasse, jeton).ConfigureAwait(false);

        try
        {
            var resultat = await ExecuterAsync(
                LocalisateurOutils.Outil(_dossierBin, "initdb"),
                [
                    "-D", _options.DossierDonnees,
                    "-U", _options.Utilisateur,
                    "--pwfile", fichierMotDePasse,
                    "--encoding", "UTF8",
                    // Tri indépendant du système, identique sur tous les postes.
                    "--locale", "C",
                    "--auth-local", "trust",
                    "--auth-host", "scram-sha-256"
                ],
                jeton).ConfigureAwait(false);

            if (resultat.CodeSortie != 0)
            {
                throw new ServeurEmbarqueException(DiagnostiquerPreparation(resultat.Sortie))
                {
                    DetailTechnique = resultat.Sortie
                };
            }
        }
        finally
        {
            TenterSuppression(fichierMotDePasse);
        }

        await RestreindreAcces(jeton).ConfigureAwait(false);
    }

    /// <summary>
    /// Traduit un échec de préparation en message utile.
    ///
    /// La sortie de PostgreSQL est en anglais et parle de « bootstrap script »
    /// et de « restricted token » : elle n'apprend rien à un commerçant. Ce
    /// message nomme les causes réelles et ce qu'il y a à faire.
    /// </summary>
    internal string DiagnostiquerPreparation(string sortie)
    {
        var message = "La base de données n'a pas pu être préparée." + Environment.NewLine;

        var accesRefuse = sortie.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
                          || sortie.Contains("Permission denied", StringComparison.OrdinalIgnoreCase);

        if (!accesRefuse)
        {
            return message + Environment.NewLine +
                   "Le détail technique a été enregistré dans le journal." + Environment.NewLine +
                   $"Dossier concerné : « {_options.DossierDonnees} ».";
        }

        message += Environment.NewLine + "Windows a refusé l'accès. Trois causes possibles :" +
                   Environment.NewLine + Environment.NewLine;

        if (EstProcessusEleve())
        {
            // Cause la plus fréquente, et la seule que l'on puisse constater.
            message += "1. LE LOGICIEL A ÉTÉ LANCÉ EN TANT QU'ADMINISTRATEUR." + Environment.NewLine +
                       "   C'est le cas ici. PostgreSQL refuse de préparer ses données dans" + Environment.NewLine +
                       "   ces conditions. Fermez le logiciel, puis rouvrez-le par un simple" + Environment.NewLine +
                       "   double-clic, sans « Exécuter en tant qu'administrateur »." + Environment.NewLine;
        }
        else
        {
            message += "1. Le logiciel a peut-être été lancé en tant qu'administrateur." + Environment.NewLine +
                       "   Rouvrez-le par un simple double-clic." + Environment.NewLine;
        }

        message += Environment.NewLine +
                   "2. L'antivirus bloque le logiciel." + Environment.NewLine +
                   "   Ajoutez une exception sur le dossier du logiciel." + Environment.NewLine +
                   Environment.NewLine +
                   "3. Le logiciel est installé à un emplacement protégé." + Environment.NewLine +
                   $"   Emplacement actuel : « {AppContext.BaseDirectory} »." + Environment.NewLine +
                   "   Déplacez le dossier dans « Documents », par exemple" + Environment.NewLine +
                   "   C:\\GestionMagasin, puis relancez.";

        return message;
    }

    /// <summary>
    /// Vrai lorsque le programme tourne avec les droits d'administrateur.
    /// PostgreSQL abandonne alors ses privilèges pour préparer ses données, et
    /// perd du même coup l'accès à ses propres fichiers.
    /// </summary>
    private static bool EstProcessusEleve()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return EstAdministrateurWindows();
    }

    [SupportedOSPlatform("windows")]
    private static bool EstAdministrateurWindows()
    {
        try
        {
            using var identite = WindowsIdentity.GetCurrent();

            return new WindowsPrincipal(identite).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Restreint l'écoute à la machine locale. Sans cela, le serveur pourrait
    /// être joignable depuis le réseau du magasin.
    /// </summary>
    private async Task RestreindreAcces(CancellationToken jeton)
    {
        var configuration = Path.Combine(_options.DossierDonnees, "postgresql.conf");

        await File.AppendAllTextAsync(
            configuration,
            Environment.NewLine +
            "# Serveur réservé au poste : aucune connexion depuis le réseau." + Environment.NewLine +
            "listen_addresses = '127.0.0.1'" + Environment.NewLine +
            $"port = {_options.Port}" + Environment.NewLine,
            jeton).ConfigureAwait(false);
    }

    /// <summary>
    /// Retient un port utilisable, à partir de celui demandé.
    ///
    /// Ouvrir le port pour de bon est le seul contrôle qui vaille : Windows
    /// réserve des plages entières pour Hyper-V, WSL et Docker et les
    /// réattribue à chaque démarrage de la machine. Un port qu'aucun
    /// programme n'occupe peut ainsi être interdit, et PostgreSQL échouer sur
    /// « Permission denied » là où il fonctionnait la veille.
    /// </summary>
    private int ChoisirPort()
    {
        var port = SelecteurPort.Premier(_options.Port, SelecteurPort.Utilisable)
            ?? throw new ServeurEmbarqueException(
                "Aucun port réseau n'est disponible pour la base de données." +
                Environment.NewLine + Environment.NewLine +
                $"Les ports {_options.Port} à {_options.Port + SelecteurPort.Etendue - 1} sont " +
                "tous refusés par Windows. Redémarrez l'ordinateur : cela suffit " +
                "presque toujours. Si le message revient, un pare-feu ou un " +
                "antivirus empêche le logiciel d'ouvrir un port local.");

        if (port != _options.Port)
        {
            Journaliser($"Le port {_options.Port} est refusé par le système : le port {port} est utilisé.");
        }

        return port;
    }

    private async Task LancerAsync(CancellationToken jeton)
    {
        var journal = Path.GetFullPath(_options.CheminJournal);
        Directory.CreateDirectory(Path.GetDirectoryName(journal)!);

        var resultat = await ExecuterAsync(
            LocalisateurOutils.Outil(_dossierBin, "pg_ctl"),
            [
                "-D", _options.DossierDonnees,
                "-l", journal,
                "-o", $"-p {PortEffectif} -h 127.0.0.1",
                "-w", "start"
            ],
            jeton).ConfigureAwait(false);

        if (resultat.CodeSortie != 0)
        {
            // La raison figure dans le journal du serveur, pas dans la sortie
            // de pg_ctl. Sans elle à l'écran, la personne devant le poste n'a
            // que « redémarrez l'ordinateur » — et si cela ne suffit pas,
            // plus rien.
            var cause = DerniereErreurDuJournal(journal);

            throw new ServeurEmbarqueException(
                "Le serveur de base de données n'a pas pu démarrer." +
                (cause is null ? string.Empty : Environment.NewLine + Environment.NewLine + "Cause : " + cause) +
                Environment.NewLine + Environment.NewLine +
                "Redémarrez l'ordinateur puis relancez le logiciel. Si le message" + Environment.NewLine +
                "revient, vérifiez que l'antivirus ne bloque pas le logiciel." + Environment.NewLine +
                Environment.NewLine +
                $"Journal du serveur : « {journal} ».")
            {
                DetailTechnique = resultat.Sortie + Environment.NewLine + LireFinDuJournal(journal)
            };
        }
    }

    /// <summary>
    /// Dernière erreur inscrite au journal du serveur, traduite lorsqu'elle
    /// fait partie des causes connues.
    ///
    /// PostgreSQL écrit en anglais. Une phrase telle que « FATAL: lock file
    /// "postmaster.pid" already exists » n'apprend rien à un commerçant, mais
    /// « une session précédente ne s'est pas fermée » lui dit quoi faire.
    /// </summary>
    private static string? DerniereErreurDuJournal(string journal)
    {
        var lignes = LireFinDuJournal(journal);

        if (string.IsNullOrWhiteSpace(lignes))
        {
            return null;
        }

        (string Motif, string Explication)[] causesConnues =
        [
            // Les motifs sont examinés dans l'ordre, du plus précis au plus
            // général : « Permission denied » apparaît aussi bien pour un port
            // refusé que pour un dossier interdit en écriture, et seule la
            // phrase complète les distingue.
            ("could not create any TCP/IP sockets",
                "Windows a refusé au logiciel l'ouverture de son port réseau local"),
            ("could not bind", "le port de la base de données n'a pas pu être ouvert"),
            ("address already in use", "le port de la base de données est déjà occupé par un autre programme"),
            ("lock file", "une session précédente ne s'est pas refermée correctement"),
            ("incompatible", "les fichiers de données proviennent d'une autre version de PostgreSQL"),
            ("invalid permissions", "le dossier de données a des droits que PostgreSQL refuse"),
            ("could not open file", "un fichier de la base de données n'a pas pu être lu"),
            ("permission denied", "le logiciel n'a pas le droit d'écrire dans son dossier de données"),
            ("administrative permissions",
                "le logiciel a été lancé en tant qu'administrateur, ce que la base de données refuse"),
            ("no space left", "le disque est plein"),
            ("data directory", "le dossier de données est inutilisable")
        ];

        foreach (var (motif, explication) in causesConnues)
        {
            if (lignes.Contains(motif, StringComparison.OrdinalIgnoreCase))
            {
                return explication + ".";
            }
        }

        // Cause inconnue : la dernière ligne « FATAL » vaut mieux que rien.
        return lignes
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(l => l.Contains("FATAL", StringComparison.OrdinalIgnoreCase)
                                || l.Contains("PANIC", StringComparison.OrdinalIgnoreCase));
    }

    private static string LireFinDuJournal(string journal, int lignes = 25)
    {
        try
        {
            if (!File.Exists(journal))
            {
                return string.Empty;
            }

            // Le journal est ouvert en partage : le serveur peut encore
            // l'écrire, et une lecture exclusive échouerait.
            using var flux = new FileStream(
                journal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var lecteur = new StreamReader(flux);

            var dernieres = new Queue<string>(lignes);

            while (lecteur.ReadLine() is { } ligne)
            {
                if (dernieres.Count == lignes)
                {
                    dernieres.Dequeue();
                }

                dernieres.Enqueue(ligne);
            }

            return string.Join(Environment.NewLine, dernieres);
        }
        catch (Exception erreur) when (erreur is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    // ==================================================================
    // Disponibilité
    // ==================================================================

    private async Task<bool> EstDisponibleAsync(CancellationToken jeton)
    {
        try
        {
            await using var connexion = new NpgsqlConnection(ChaineAdministration("postgres", ObtenirOuCreerMotDePasse()));
            await connexion.OpenAsync(jeton).ConfigureAwait(false);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task AttendreDisponibiliteAsync(CancellationToken jeton)
    {
        var limite = DateTime.UtcNow + _options.DelaiDemarrage;

        while (DateTime.UtcNow < limite)
        {
            if (await EstDisponibleAsync(jeton).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(250, jeton).ConfigureAwait(false);
        }

        throw new ServeurEmbarqueException(
            $"Le serveur de base de données n'a pas répondu en " +
            $"{_options.DelaiDemarrage.TotalSeconds:N0} secondes.");
    }

    /// <summary>Crée la base du magasin si elle n'existe pas encore.</summary>
    private async Task GarantirBaseAsync(string motDePasse, CancellationToken jeton)
    {
        await using var connexion = new NpgsqlConnection(ChaineAdministration("postgres", motDePasse));
        await connexion.OpenAsync(jeton).ConfigureAwait(false);

        await using (var verification = connexion.CreateCommand())
        {
            verification.CommandText = "SELECT 1 FROM pg_database WHERE datname = @nom";
            verification.Parameters.AddWithValue("nom", _options.NomBase);

            if (await verification.ExecuteScalarAsync(jeton).ConfigureAwait(false) is not null)
            {
                return;
            }
        }

        // Le nom vient de la configuration du logiciel, jamais d'une saisie :
        // il est tout de même échappé, CREATE DATABASE n'acceptant pas de
        // paramètre.
        await using var creation = connexion.CreateCommand();
        creation.CommandText = $"CREATE DATABASE \"{_options.NomBase.Replace("\"", "\"\"")}\"";
        await creation.ExecuteNonQueryAsync(jeton).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.GetFullPath(CheminMarqueurBase),
            DateTime.UtcNow.ToString("O"),
            jeton).ConfigureAwait(false);

        Journaliser($"Base « {_options.NomBase} » créée.");
    }

    // ==================================================================
    // Accès
    // ==================================================================

    internal string Chaine(string base_, string motDePasse) =>
        Construire(base_, motDePasse, pooling: true);

    /// <summary>
    /// Chaîne réservée aux opérations de service de cette classe.
    ///
    /// La réutilisation des connexions y est désactivée : ces connexions
    /// encadrent des arrêts et des démarrages du serveur, et une connexion
    /// gardée en réserve pointerait vers un serveur qui n'existe plus. Elle
    /// serait alors distribuée telle quelle, provoquant l'erreur « terminating
    /// connection due to administrator command ».
    /// </summary>
    private string ChaineAdministration(string base_, string motDePasse) =>
        Construire(base_, motDePasse, pooling: false);

    private string Construire(string base_, string motDePasse, bool pooling) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = PortEffectif,
            Database = base_,
            Username = _options.Utilisateur,
            Password = motDePasse,
            Timeout = 10,
            Pooling = pooling,
            IncludeErrorDetail = true
        }.ConnectionString;

    /// <summary>
    /// Lit le mot de passe du serveur, ou en tire un au premier démarrage.
    /// Personne ne le saisit ni n'a besoin de le retenir : il ne sert qu'à
    /// cette application, sur cette machine.
    /// </summary>
    private string ObtenirOuCreerMotDePasse()
    {
        var chemin = Path.GetFullPath(CheminMotDePasse);

        if (File.Exists(chemin))
        {
            var existant = File.ReadAllText(chemin).Trim();

            if (existant.Length > 0)
            {
                return existant;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);

        var motDePasse = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace("+", "").Replace("/", "").Replace("=", "");

        File.WriteAllText(chemin, motDePasse, new UTF8Encoding(false));

        return motDePasse;
    }

    // ==================================================================
    // Utilitaires
    // ==================================================================

    internal readonly record struct ResultatCommande(int CodeSortie, string Sortie);

    internal async Task<ResultatCommande> ExecuterAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken jeton,
        IReadOnlyDictionary<string, string>? variables = null)
    {
        if (!File.Exists(executable))
        {
            throw new ServeurEmbarqueException($"Programme introuvable : « {executable} ».");
        }

        var demarrage = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            demarrage.ArgumentList.Add(argument);
        }

        if (variables is not null)
        {
            foreach (var (cle, valeur) in variables)
            {
                demarrage.Environment[cle] = valeur;
            }
        }

        Process? processus;

        try
        {
            processus = Process.Start(demarrage);
        }
        catch (System.ComponentModel.Win32Exception refus)
        {
            // Windows refuse le lancement. Le cas le plus courant est un
            // emplacement que le logiciel n'a pas le droit d'exécuter.
            throw new ServeurEmbarqueException(
                "Windows a refusé de lancer la base de données." + Environment.NewLine +
                Environment.NewLine +
                "Déplacez le dossier du logiciel vers un emplacement simple, par" + Environment.NewLine +
                "exemple C:\\GestionMagasin, en évitant les caractères inhabituels" + Environment.NewLine +
                "(« ! », « # », « % »), les dossiers synchronisés et les lecteurs réseau." +
                Environment.NewLine + Environment.NewLine +
                $"Emplacement actuel : « {AppContext.BaseDirectory} ».",
                refus)
            {
                DetailTechnique = $"{executable}{Environment.NewLine}{refus.Message}"
            };
        }

        if (processus is null)
        {
            throw new ServeurEmbarqueException($"Impossible de lancer « {executable} ».");
        }

        using var _ = processus;

        var lectureSortie = processus.StandardOutput.ReadToEndAsync(jeton);
        var lectureErreurs = processus.StandardError.ReadToEndAsync(jeton);

        await processus.WaitForExitAsync(jeton).ConfigureAwait(false);

        // La fin des programmes appelés ne signifie pas la fermeture de leurs
        // tuyaux de sortie. Sous Windows, « pg_ctl start » laisse le serveur
        // qu'il démarre en hériter : ces tuyaux restent ouverts tant que le
        // serveur tourne, c'est-à-dire indéfiniment. Attendre qu'ils se
        // ferment bloquerait le logiciel au démarrage.
        var texte = await LireCeQuiEstDisponibleAsync(lectureSortie, lectureErreurs)
            .ConfigureAwait(false);

        return new ResultatCommande(processus.ExitCode, texte);
    }

    /// <summary>
    /// Récupère la sortie des programmes appelés sans jamais s'y bloquer.
    /// Ce texte ne sert qu'à rédiger les messages d'erreur : mieux vaut un
    /// diagnostic incomplet qu'une application figée.
    /// </summary>
    private static async Task<string> LireCeQuiEstDisponibleAsync(
        Task<string> sortie,
        Task<string> erreurs)
    {
        var lectures = Task.WhenAll(sortie, erreurs);

        await Task.WhenAny(lectures, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);

        static string Recuperer(Task<string> lecture) =>
            lecture.IsCompletedSuccessfully ? lecture.Result : string.Empty;

        return (Recuperer(sortie) + Environment.NewLine + Recuperer(erreurs)).Trim();
    }

    internal OptionsServeur Options => _options;

    internal string MotDePasseCourant() => ObtenirOuCreerMotDePasse();

    private static void TenterSuppression(string chemin)
    {
        try
        {
            File.Delete(chemin);
        }
        catch (IOException)
        {
            // Fichier temporaire : son maintien n'empêche rien.
        }
    }

    private void Journaliser(string message) => _journaliser?.Invoke(message);
}
