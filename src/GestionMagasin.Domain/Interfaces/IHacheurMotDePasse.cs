namespace GestionMagasin.Domain.Interfaces;

/// <summary>
/// Transforme un mot de passe en empreinte irréversible et vérifie une
/// saisie face à une empreinte enregistrée. Aucun mot de passe n'est jamais
/// conservé ni journalisé en clair.
/// </summary>
public interface IHacheurMotDePasse
{
    /// <summary>Calcule l'empreinte salée d'un mot de passe.</summary>
    string Hacher(string motDePasse);

    /// <summary>
    /// Vérifie qu'un mot de passe correspond à une empreinte. La comparaison
    /// est faite en temps constant pour ne pas exposer d'information.
    /// </summary>
    bool Verifier(string motDePasse, string empreinte);

    /// <summary>
    /// Indique qu'une empreinte a été produite avec des paramètres devenus
    /// trop faibles et mérite d'être recalculée à la prochaine connexion.
    /// </summary>
    bool NecessiteMiseAJour(string empreinte);
}
