namespace GestionMagasin.Application.Common;

/// <summary>
/// Où se trouve le logo du magasin. L'écran et les documents PDF le
/// cherchent aux mêmes endroits, dans le même ordre :
/// 1. le dossier de données du poste, où l'écran Paramètres l'installe ;
/// 2. à côté du programme, où le script de publication le dépose.
///
/// Le premier l'emporte : un magasin peut ainsi changer son logo lui-même
/// sans toucher au dossier d'installation, qui est parfois protégé par
/// Windows.
/// </summary>
public static class EmplacementsLogo
{
    /// <summary>Noms acceptés à côté du programme, par ordre de préférence.</summary>
    private static readonly string[] NomsFichiers = ["logo.png", "logo.jpg", "logo.jpeg", "logo.bmp"];

    /// <summary>Emplacement du logo installé depuis l'écran Paramètres.</summary>
    public static string CheminInstalle { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GestionMagasin",
        "logo.png");

    /// <summary>Emplacements possibles, du plus prioritaire au moins prioritaire.</summary>
    public static IEnumerable<string> Candidats()
    {
        yield return CheminInstalle;

        foreach (var nom in NomsFichiers)
        {
            yield return Path.Combine(AppContext.BaseDirectory, nom);
        }
    }
}
