using GestionMagasin.Application.Common;
using GestionMagasin.Application.Services.Abstractions;

namespace GestionMagasin.Infrastructure.Documents;

/// <summary>
/// Logo lu sur le disque, aux emplacements où l'écran le trouve aussi. Le
/// fichier est relu à chaque document : un logo changé dans Paramètres
/// figure sur le ticket suivant, sans redémarrage.
/// </summary>
public sealed class LogoMagasinFichier : ILogoMagasin
{
    public byte[]? Lire()
    {
        foreach (var chemin in EmplacementsLogo.Candidats())
        {
            try
            {
                if (File.Exists(chemin))
                {
                    return File.ReadAllBytes(chemin);
                }
            }
            catch (Exception erreur) when (erreur is IOException or UnauthorizedAccessException)
            {
                // Fichier verrouillé ou interdit : l'emplacement suivant.
            }
        }

        return null;
    }
}
