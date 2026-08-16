namespace GestionMagasin.Domain.Securite;

/// <summary>
/// Compte créé lors de la toute première installation, le temps que le
/// magasin crée ses propres utilisateurs. Le logiciel signale à la connexion
/// que ce mot de passe doit être remplacé.
/// </summary>
public static class ComptesParDefaut
{
    public const string NomUtilisateurAdministrateur = "admin";

    public const string MotDePasseAdministrateur = "Admin@2026";
}
