using System.IO;
using System.Windows.Media.Imaging;

namespace GestionMagasin.App.Views;

/// <summary>
/// Logo déposé par le magasin.
///
/// Le blason dessiné par <see cref="Blason"/> s'affiche toujours, sans
/// dépendre d'aucun fichier. Si le magasin dépose son propre logo à côté du
/// programme, c'est lui qui est repris : chaque enseigne garde ainsi son
/// identité sans qu'il faille recompiler le logiciel.
/// </summary>
internal static class Enseigne
{
    /// <summary>Noms acceptés pour le logo du magasin, par ordre de préférence.</summary>
    private static readonly string[] NomsFichiers = ["logo.png", "logo.jpg", "logo.jpeg", "logo.bmp"];

    private static readonly Lazy<BitmapImage?> LogoCharge = new(Charger);

    /// <summary>Logo déposé par le magasin, ou null s'il n'y en a pas.</summary>
    public static BitmapImage? Logo => LogoCharge.Value;

    /// <summary>Vrai lorsqu'un logo a été trouvé : le blason dessiné s'efface alors.</summary>
    public static bool LogoPresent => Logo is not null;

    private static BitmapImage? Charger()
    {
        foreach (var nom in NomsFichiers)
        {
            var chemin = Path.Combine(AppContext.BaseDirectory, nom);

            if (!File.Exists(chemin))
            {
                continue;
            }

            try
            {
                var image = new BitmapImage();

                image.BeginInit();
                // Le fichier est copié en mémoire : sans cela il resterait
                // verrouillé, et le magasin ne pourrait plus le remplacer.
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(chemin);
                image.EndInit();
                image.Freeze();

                return image;
            }
            catch (Exception)
            {
                // Fichier illisible ou abîmé : le blason dessiné prend le relais.
            }
        }

        return null;
    }
}
