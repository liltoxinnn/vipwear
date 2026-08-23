namespace GestionMagasin.ServeurEmbarque;

/// <summary>Résultat d'une sauvegarde ou d'une restauration.</summary>
public sealed record ResultatSauvegarde(bool Reussie, string Message, string? Chemin = null);

/// <summary>
/// Sauvegarde et restauration des données du magasin.
///
/// Ce service est indispensable dès lors que PostgreSQL est embarqué : pgAdmin
/// n'est alors pas installé sur le poste, et le magasin n'aurait plus aucun
/// moyen de protéger son historique. Un disque qui lâche emporterait sinon
/// tout le fichier clients et toutes les ventes.
/// </summary>
public sealed class ServiceSauvegarde
{
    private readonly ServeurPostgresEmbarque _serveur;

    public ServiceSauvegarde(ServeurPostgresEmbarque serveur)
    {
        _serveur = serveur;
    }

    /// <summary>Nom de fichier proposé par défaut, daté du jour.</summary>
    public static string NomFichierPropose() =>
        $"sauvegarde-magasin-{DateTime.Now:yyyy-MM-dd-HHmm}.sauvegarde";

    /// <summary>
    /// Écrit une sauvegarde complète de la base du magasin.
    /// Le format « custom » de PostgreSQL est utilisé : compressé, et
    /// restaurable même si la structure a évolué entre-temps.
    /// </summary>
    public async Task<ResultatSauvegarde> SauvegarderAsync(
        string cheminDestination,
        CancellationToken jeton = default)
    {
        var dossier = Path.GetDirectoryName(Path.GetFullPath(cheminDestination));

        if (!string.IsNullOrEmpty(dossier))
        {
            Directory.CreateDirectory(dossier);
        }

        var resultat = await _serveur.ExecuterAsync(
            LocalisateurOutils.Outil(_serveur.DossierOutils, "pg_dump"),
            [
                "--host", "127.0.0.1",
                "--port", _serveur.Options.Port.ToString(),
                "--username", _serveur.Options.Utilisateur,
                "--format", "custom",
                "--no-password",
                "--file", cheminDestination,
                _serveur.Options.NomBase
            ],
            jeton,
            MotDePasseParEnvironnement()).ConfigureAwait(false);

        if (resultat.CodeSortie != 0)
        {
            return new ResultatSauvegarde(
                false,
                "La sauvegarde a échoué." + Environment.NewLine + resultat.Sortie);
        }

        var taille = new FileInfo(cheminDestination).Length;

        return new ResultatSauvegarde(
            true,
            $"Sauvegarde enregistrée ({taille / 1024d / 1024d:N1} Mo).",
            cheminDestination);
    }

    /// <summary>
    /// Remplace le contenu de la base par celui d'une sauvegarde.
    ///
    /// L'opération est destructrice : tout ce qui a été saisi depuis la
    /// sauvegarde est perdu. L'appelant doit avoir fait confirmer.
    /// </summary>
    public async Task<ResultatSauvegarde> RestaurerAsync(
        string cheminSauvegarde,
        CancellationToken jeton = default)
    {
        if (!File.Exists(cheminSauvegarde))
        {
            return new ResultatSauvegarde(false, "Le fichier de sauvegarde est introuvable.");
        }

        // --clean supprime les objets existants avant de les recréer, sinon la
        // restauration échouerait sur chaque table déjà présente.
        var resultat = await _serveur.ExecuterAsync(
            LocalisateurOutils.Outil(_serveur.DossierOutils, "pg_restore"),
            [
                "--host", "127.0.0.1",
                "--port", _serveur.Options.Port.ToString(),
                "--username", _serveur.Options.Utilisateur,
                "--dbname", _serveur.Options.NomBase,
                "--clean",
                "--if-exists",
                "--no-owner",
                "--no-password",
                cheminSauvegarde
            ],
            jeton,
            MotDePasseParEnvironnement()).ConfigureAwait(false);

        // pg_restore signale par un code non nul des avertissements sans
        // gravité, par exemple la suppression d'un objet absent. Seule
        // l'absence de données restaurées serait un vrai échec.
        if (resultat.CodeSortie != 0 && resultat.Sortie.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return new ResultatSauvegarde(
                false,
                "La restauration a échoué." + Environment.NewLine + resultat.Sortie);
        }

        return new ResultatSauvegarde(true, "Données restaurées.", cheminSauvegarde);
    }

    /// <summary>
    /// Le mot de passe passe par l'environnement du processus appelé, jamais
    /// par la ligne de commande, que les autres processus peuvent lire.
    /// </summary>
    private Dictionary<string, string> MotDePasseParEnvironnement() =>
        new() { ["PGPASSWORD"] = _serveur.MotDePasseCourant() };
}
