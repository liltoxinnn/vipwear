namespace GestionMagasin.ServeurEmbarque;

/// <summary>
/// Décide si le fichier « postmaster.pid » laissé dans un dossier de données
/// protège encore un serveur, ou s'il est le reste d'un arrêt brutal.
///
/// PostgreSQL écrit ce verrou au démarrage et l'efface en s'arrêtant. Après
/// une coupure de courant, un « taskkill » ou un plantage, il subsiste et le
/// serveur refuse de démarrer tant qu'il est là.
///
/// La décision est isolée ici parce qu'elle est facile à prendre à moitié :
/// constater qu'un processus porte le numéro inscrit ne suffit pas. Windows
/// réattribue les numéros, et après un redémarrage celui d'un serveur mort
/// désigne le plus souvent un navigateur ou un jeu. Le verrou paraissait
/// alors valide, et le magasin restait bloqué sur « Le serveur de base de
/// données n'a pas pu démarrer » jusqu'à ce que quelqu'un efface le fichier
/// à la main.
/// </summary>
public static class VerrouGrappe
{
    /// <summary>Nom du processus d'un serveur PostgreSQL.</summary>
    public const string NomProcessusServeur = "postgres";

    /// <summary>
    /// Vrai lorsque le verrou ne protège plus rien et peut être retiré.
    /// </summary>
    /// <param name="lignesVerrou">Contenu du fichier « postmaster.pid ».</param>
    /// <param name="dossierDonnees">Dossier de données de notre serveur.</param>
    /// <param name="nomDuProcessus">
    /// Nom du processus portant un numéro donné, ou null s'il n'existe plus.
    /// </param>
    public static bool EstPerime(
        IReadOnlyList<string> lignesVerrou,
        string dossierDonnees,
        Func<int, string?> nomDuProcessus)
    {
        // Un verrou vide ou illisible ne protège rien.
        if (lignesVerrou.Count == 0 || !int.TryParse(lignesVerrou[0].Trim(), out var identifiant))
        {
            return true;
        }

        // Deuxième ligne : le dossier de données du serveur qui l'a écrit.
        // S'il n'est pas le nôtre, ce verrou concerne une autre grappe — et
        // nous n'avons rien à faire dans son dossier.
        if (lignesVerrou.Count > 1 && !string.IsNullOrWhiteSpace(lignesVerrou[1])
            && !CheminsEquivalents(lignesVerrou[1], dossierDonnees))
        {
            return false;
        }

        var nom = nomDuProcessus(identifiant);

        // Plus aucun processus : le verrou est périmé.
        // Un processus, mais qui n'est pas un serveur : le numéro a été
        // réattribué, le verrou est périmé aussi.
        return nom is null
               || !nom.Equals(NomProcessusServeur, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compare deux chemins écrits par le même système.
    ///
    /// La normalisation est faite ici plutôt que confiée à
    /// <see cref="Path.GetFullPath(string)"/> : le verrou contient toujours un
    /// chemin Windows, alors que les contrôles s'exécutent aussi ailleurs, où
    /// la barre inverse n'est pas un séparateur mais un caractère de nom de
    /// fichier ordinaire. La comparaison serait alors faussée sur la machine
    /// même qui doit la vérifier.
    /// </summary>
    private static bool CheminsEquivalents(string premier, string second) =>
        string.Equals(Normaliser(premier), Normaliser(second), StringComparison.OrdinalIgnoreCase);

    private static string Normaliser(string chemin)
    {
        var segments = new List<string>();

        foreach (var segment in chemin.Trim().Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            switch (segment)
            {
                case ".":
                    continue;

                case ".." when segments.Count > 0:
                    segments.RemoveAt(segments.Count - 1);
                    continue;

                case "..":
                    continue;

                default:
                    segments.Add(segment);
                    continue;
            }
        }

        return string.Join('/', segments);
    }
}
