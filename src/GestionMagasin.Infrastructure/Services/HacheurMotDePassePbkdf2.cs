using System.Security.Cryptography;
using GestionMagasin.Domain.Interfaces;

namespace GestionMagasin.Infrastructure.Services;

/// <summary>
/// Hachage des mots de passe avec PBKDF2-HMAC-SHA512.
/// Chaque mot de passe reçoit un sel aléatoire de 128 bits, si bien que deux
/// comptes ayant le même mot de passe produisent des empreintes différentes.
/// Le nombre d'itérations est stocké dans l'empreinte : il peut être relevé
/// dans une version future sans invalider les comptes existants.
/// Format : « PBKDF2-SHA512$itérations$sel$empreinte », en Base64.
/// </summary>
public class HacheurMotDePassePbkdf2 : IHacheurMotDePasse
{
    private const string Algorithme = "PBKDF2-SHA512";
    private const int IterationsActuelles = 210_000;
    private const int TailleSelOctets = 16;
    private const int TailleEmpreinteOctets = 32;
    private const char Separateur = '$';

    public string Hacher(string motDePasse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motDePasse);

        var sel = RandomNumberGenerator.GetBytes(TailleSelOctets);
        var empreinte = Deriver(motDePasse, sel, IterationsActuelles);

        return string.Join(Separateur,
            Algorithme,
            IterationsActuelles.ToString(),
            Convert.ToBase64String(sel),
            Convert.ToBase64String(empreinte));
    }

    public bool Verifier(string motDePasse, string empreinteEnregistree)
    {
        if (string.IsNullOrWhiteSpace(motDePasse) || string.IsNullOrWhiteSpace(empreinteEnregistree))
        {
            return false;
        }

        if (!TryAnalyser(empreinteEnregistree, out var iterations, out var sel, out var empreinteAttendue))
        {
            return false;
        }

        var empreinteCalculee = Deriver(motDePasse, sel, iterations);

        // Comparaison à temps constant : la durée de la vérification ne
        // renseigne pas un attaquant sur le nombre d'octets corrects.
        return CryptographicOperations.FixedTimeEquals(empreinteCalculee, empreinteAttendue);
    }

    public bool NecessiteMiseAJour(string empreinteEnregistree)
    {
        if (!TryAnalyser(empreinteEnregistree, out var iterations, out _, out _))
        {
            return true;
        }

        return iterations < IterationsActuelles;
    }

    private static byte[] Deriver(string motDePasse, byte[] sel, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            motDePasse,
            sel,
            iterations,
            HashAlgorithmName.SHA512,
            TailleEmpreinteOctets);

    private static bool TryAnalyser(
        string empreinteEnregistree,
        out int iterations,
        out byte[] sel,
        out byte[] empreinte)
    {
        iterations = 0;
        sel = Array.Empty<byte>();
        empreinte = Array.Empty<byte>();

        var parties = empreinteEnregistree.Split(Separateur);

        if (parties.Length != 4 || parties[0] != Algorithme)
        {
            return false;
        }

        if (!int.TryParse(parties[1], out iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            sel = Convert.FromBase64String(parties[2]);
            empreinte = Convert.FromBase64String(parties[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return sel.Length > 0 && empreinte.Length > 0;
    }
}
