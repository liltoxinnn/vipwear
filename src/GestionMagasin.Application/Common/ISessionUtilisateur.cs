namespace GestionMagasin.Application.Common;

/// <summary>
/// Identité de l'utilisateur connecté et autorisations dont il dispose.
/// Les services consultent cette session pour tracer les actions et pour
/// refuser celles qui ne sont pas permises : le contrôle ne repose jamais
/// uniquement sur le masquage des boutons dans l'interface.
/// </summary>
public interface ISessionUtilisateur
{
    int? UtilisateurId { get; }

    string NomUtilisateur { get; }

    string NomComplet { get; }

    string NomRole { get; }

    bool EstAuthentifie { get; }

    /// <summary>Indique si l'utilisateur connecté possède une permission.</summary>
    bool Possede(string codePermission);

    /// <summary>
    /// Vérifie une permission et interrompt l'opération si elle manque.
    /// </summary>
    /// <exception cref="Domain.Exceptions.AccesRefuseException">
    /// Levée lorsque la permission n'est pas accordée.
    /// </exception>
    void ExigerPermission(string codePermission);

    /// <summary>Identifiant de l'utilisateur connecté, ou erreur si personne ne l'est.</summary>
    int UtilisateurIdObligatoire { get; }
}
