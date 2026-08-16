namespace GestionMagasin.Domain.Exceptions;

/// <summary>
/// Regroupe les erreurs de saisie détectées avant l'enregistrement.
/// Toutes les erreurs sont remontées d'un coup pour éviter à l'utilisateur
/// de corriger son formulaire champ après champ.
/// </summary>
public class ValidationMetierException : ExceptionMetier
{
    public ValidationMetierException(IReadOnlyList<string> erreurs)
        : base(ConstruireMessage(erreurs))
    {
        Erreurs = erreurs;
    }

    public ValidationMetierException(string erreur) : this(new[] { erreur })
    {
    }

    public IReadOnlyList<string> Erreurs { get; }

    private static string ConstruireMessage(IReadOnlyList<string> erreurs)
    {
        if (erreurs.Count == 1)
        {
            return erreurs[0];
        }

        return "Veuillez corriger les points suivants :" + Environment.NewLine +
               string.Join(Environment.NewLine, erreurs.Select(e => $"• {e}"));
    }
}
