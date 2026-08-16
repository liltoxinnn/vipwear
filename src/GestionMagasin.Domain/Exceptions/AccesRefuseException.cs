namespace GestionMagasin.Domain.Exceptions;

/// <summary>
/// Levée lorsqu'un utilisateur tente une action que son rôle ne l'autorise
/// pas à réaliser. Le contrôle est fait dans les services, pas seulement dans
/// l'interface, afin qu'il ne puisse pas être contourné.
/// </summary>
public class AccesRefuseException : ExceptionMetier
{
    public AccesRefuseException(string permissionRequise)
        : base("Vous n'avez pas l'autorisation d'effectuer cette action. Contactez votre administrateur.")
    {
        PermissionRequise = permissionRequise;
    }

    public string PermissionRequise { get; }
}
