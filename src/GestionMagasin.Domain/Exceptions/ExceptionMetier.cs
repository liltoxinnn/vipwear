namespace GestionMagasin.Domain.Exceptions;

/// <summary>
/// Erreur fonctionnelle prévue par le logiciel. Son message est rédigé en
/// français et peut être affiché tel quel à l'utilisateur, contrairement aux
/// exceptions techniques qui sont journalisées puis remplacées par un message
/// générique.
/// </summary>
public class ExceptionMetier : Exception
{
    public ExceptionMetier(string message) : base(message)
    {
    }

    public ExceptionMetier(string message, Exception innerException) : base(message, innerException)
    {
    }
}
