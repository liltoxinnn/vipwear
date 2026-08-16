namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>Résultat d'une tentative de connexion.</summary>
public record ResultatConnexion(bool Reussie, string? Message, bool MotDePasseAChanger = false)
{
    public static ResultatConnexion Succes(bool motDePasseAChanger = false) =>
        new(true, null, motDePasseAChanger);

    public static ResultatConnexion Echec(string message) => new(false, message);
}

/// <summary>Ouverture et fermeture de session, et gestion du mot de passe personnel.</summary>
public interface IServiceAuthentification
{
    /// <summary>
    /// Vérifie les identifiants et ouvre la session. Le message d'erreur reste
    /// volontairement identique quel que soit le motif de l'échec, afin de ne
    /// pas révéler l'existence d'un compte.
    /// </summary>
    Task<ResultatConnexion> ConnecterAsync(
        string nomUtilisateur,
        string motDePasse,
        CancellationToken jeton = default);

    /// <summary>Ferme la session en cours.</summary>
    Task DeconnecterAsync(CancellationToken jeton = default);

    /// <summary>Change le mot de passe de l'utilisateur connecté après vérification de l'ancien.</summary>
    Task ChangerMotDePasseAsync(
        string ancienMotDePasse,
        string nouveauMotDePasse,
        CancellationToken jeton = default);
}
