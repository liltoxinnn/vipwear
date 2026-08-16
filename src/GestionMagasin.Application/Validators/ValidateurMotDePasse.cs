using GestionMagasin.Domain.Exceptions;

namespace GestionMagasin.Application.Validators;

/// <summary>
/// Règles de robustesse imposées aux mots de passe. Elles sont volontairement
/// simples à expliquer au personnel du magasin, tout en écartant les mots de
/// passe les plus faciles à deviner.
/// </summary>
public static class ValidateurMotDePasse
{
    public const int LongueurMinimale = 8;

    /// <summary>Mots de passe trop courants pour être acceptés.</summary>
    private static readonly HashSet<string> MotsDePasseInterdits = new(StringComparer.OrdinalIgnoreCase)
    {
        "motdepasse", "password", "12345678", "azertyuiop", "qwertyuiop",
        "admin123", "administrateur", "magasin123", "00000000", "11111111"
    };

    /// <summary>Vérifie un mot de passe et lève une exception détaillée si besoin.</summary>
    public static void Valider(string motDePasse)
    {
        var erreurs = Controler(motDePasse);

        if (erreurs.Count > 0)
        {
            throw new ValidationMetierException(erreurs);
        }
    }

    /// <summary>Retourne la liste des règles non respectées, en français.</summary>
    public static IReadOnlyList<string> Controler(string? motDePasse)
    {
        var erreurs = new List<string>();

        if (string.IsNullOrWhiteSpace(motDePasse))
        {
            erreurs.Add("Le mot de passe est obligatoire.");
            return erreurs;
        }

        if (motDePasse.Length < LongueurMinimale)
        {
            erreurs.Add($"Le mot de passe doit contenir au moins {LongueurMinimale} caractères.");
        }

        if (!motDePasse.Any(char.IsLetter))
        {
            erreurs.Add("Le mot de passe doit contenir au moins une lettre.");
        }

        if (!motDePasse.Any(char.IsDigit))
        {
            erreurs.Add("Le mot de passe doit contenir au moins un chiffre.");
        }

        if (MotsDePasseInterdits.Contains(motDePasse))
        {
            erreurs.Add("Ce mot de passe est trop courant. Veuillez en choisir un autre.");
        }

        return erreurs;
    }
}
