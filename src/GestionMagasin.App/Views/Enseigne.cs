using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using GestionMagasin.Application.Common;

namespace GestionMagasin.App.Views;

/// <summary>
/// Logo du magasin.
///
/// L'emblème dessiné par <see cref="Blason"/> s'affiche toujours, sans
/// dépendre d'aucun fichier : le logiciel ne peut donc pas être livré sans
/// enseigne. Si le magasin fournit son propre logo, c'est lui qui est repris
/// partout — écran de connexion et menu — sans qu'il faille recompiler.
///
/// Le fichier est cherché aux emplacements de <see cref="EmplacementsLogo"/>,
/// les mêmes que pour les tickets en PDF.
/// </summary>
public sealed partial class Enseigne : ObservableObject
{
    private Enseigne() => Recharger();

    /// <summary>
    /// Enseigne du poste. Les gabarits s'y lient : remplacer le logo met donc
    /// à jour l'écran de connexion et le menu sans redémarrage.
    /// </summary>
    public static Enseigne Courante { get; } = new();

    /// <summary>Emplacement du logo installé depuis l'écran Paramètres.</summary>
    public static string CheminInstalle => EmplacementsLogo.CheminInstalle;

    [ObservableProperty]
    private BitmapImage? _logo;

    /// <summary>Vrai lorsqu'un logo est disponible : l'emblème dessiné s'efface alors.</summary>
    public bool LogoPresent => Logo is not null;

    /// <summary>Vrai lorsque le logo affiché a été installé depuis le logiciel.</summary>
    public bool LogoInstalle => File.Exists(CheminInstalle);

    /// <summary>
    /// Installe une image comme logo du magasin.
    ///
    /// L'image est d'abord lue : un fichier abîmé ou d'un format inconnu est
    /// refusé avant d'être copié, faute de quoi le magasin se retrouverait
    /// sans aucune enseigne.
    /// </summary>
    /// <param name="chemin">Image choisie par l'utilisateur.</param>
    /// <returns>Vrai si le logo a été installé.</returns>
    public bool Installer(string chemin)
    {
        if (Lire(chemin) is null)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(CheminInstalle)!);
        File.Copy(chemin, CheminInstalle, overwrite: true);

        Recharger();

        return LogoPresent;
    }

    /// <summary>Retire le logo installé et revient à l'emblème dessiné.</summary>
    public void Retirer()
    {
        if (File.Exists(CheminInstalle))
        {
            File.Delete(CheminInstalle);
        }

        Recharger();
    }

    /// <summary>Relit le logo depuis le disque.</summary>
    public void Recharger() => Logo = Chercher();

    partial void OnLogoChanged(BitmapImage? value)
    {
        OnPropertyChanged(nameof(LogoPresent));
        OnPropertyChanged(nameof(LogoInstalle));
    }

    private static BitmapImage? Chercher()
    {
        foreach (var chemin in EmplacementsLogo.Candidats())
        {
            if (Lire(chemin) is { } image)
            {
                return image;
            }
        }

        return null;
    }

    private static BitmapImage? Lire(string chemin)
    {
        if (!File.Exists(chemin))
        {
            return null;
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
            // Fichier illisible ou abîmé : l'emblème dessiné prend le relais.
            return null;
        }
    }
}
