using System.IO;
using System.Windows.Media.Imaging;

namespace GestionMagasin.App.Views;

/// <summary>
/// Emblème du magasin.
///
/// Le blason dessiné plus bas s'affiche toujours, sans dépendre d'aucun
/// fichier. Si le magasin dépose son propre logo à côté du programme, c'est
/// lui qui est repris : chaque enseigne garde ainsi son identité sans qu'il
/// faille recompiler le logiciel.
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

    /// <summary>
    /// Couronne du blason, reprise de l'enseigne. Dessinée dans un carré de
    /// 24 sur 24, comme les icônes du menu.
    /// </summary>
    public const string Couronne =
        "M3.2,17.4 L5.4,7.2 L9.3,11.9 L12,4.6 L14.7,11.9 L18.6,7.2 L20.8,17.4 Z " +
        "M3.6,18.9 H20.4 V21 H3.6 Z";

    /// <summary>Les trois étoiles du pourtour, alignées horizontalement.</summary>
    public const string Etoile =
        "M12,3 L13.9,8.8 H20 L15.1,12.4 L16.9,18.2 L12,14.6 L7.1,18.2 L8.9,12.4 L4,8.8 H10.1 Z";
}
