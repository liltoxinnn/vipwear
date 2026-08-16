namespace GestionMagasin.Domain.Exceptions;

/// <summary>Levée lorsqu'un enregistrement attendu n'existe pas ou plus.</summary>
public class EntiteIntrouvableException : ExceptionMetier
{
    public EntiteIntrouvableException(string typeEntite, object identifiant)
        : base($"{typeEntite} introuvable (identifiant : {identifiant}). L'élément a peut-être été supprimé.")
    {
        TypeEntite = typeEntite;
        Identifiant = identifiant;
    }

    public EntiteIntrouvableException(string message) : base(message)
    {
        TypeEntite = string.Empty;
        Identifiant = string.Empty;
    }

    public string TypeEntite { get; }

    public object Identifiant { get; }
}
