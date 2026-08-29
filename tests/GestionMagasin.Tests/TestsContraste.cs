using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Contraste des textes sur fond clair.
///
/// Un texte trop pâle ne se lit pas comme du gris : il se lit comme du flou.
/// À onze pixels et demi, l'œil ne distingue plus le tracé de la lettre du
/// fond qui l'entoure, et le magasin conclut que l'écran est mal affiché. Le
/// seuil retenu est celui des recommandations d'accessibilité — quatre et
/// demi pour un — au-dessous duquel un texte courant devient pénible.
/// </summary>
public class TestsContraste
{
    private const double SeuilTextePetit = 4.5d;

    private static string FichierCouleurs
    {
        get
        {
            var dossier = new DirectoryInfo(AppContext.BaseDirectory);

            while (dossier is not null)
            {
                var candidat = Path.Combine(
                    dossier.FullName, "src", "GestionMagasin.App", "Resources", "Couleurs.xaml");

                if (File.Exists(candidat))
                {
                    return candidat;
                }

                dossier = dossier.Parent;
            }

            throw new FileNotFoundException("Couleurs.xaml est introuvable.");
        }
    }

    private static Dictionary<string, string> Palette()
    {
        var contenu = File.ReadAllText(FichierCouleurs);
        var palette = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match correspondance in Regex.Matches(
                     contenu, @"<Color\s+x:Key=""([^""]+)""\s*>\s*(#[0-9A-Fa-f]{6})\s*</Color>"))
        {
            palette[correspondance.Groups[1].Value] = correspondance.Groups[2].Value;
        }

        return palette;
    }

    /// <summary>Luminance relative, telle que définie par les recommandations.</summary>
    private static double Luminance(string couleur)
    {
        static double Canal(int valeur)
        {
            var v = valeur / 255d;

            return v <= 0.03928d ? v / 12.92d : Math.Pow((v + 0.055d) / 1.055d, 2.4d);
        }

        var r = int.Parse(couleur.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(couleur.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(couleur.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return 0.2126d * Canal(r) + 0.7152d * Canal(g) + 0.0722d * Canal(b);
    }

    private static double Contraste(string gauche, string droite)
    {
        var a = Luminance(gauche);
        var b = Luminance(droite);

        return (Math.Max(a, b) + 0.05d) / (Math.Min(a, b) + 0.05d);
    }

    [Fact]
    public void Chaque_teinte_de_texte_se_detache_du_fond_des_panneaux()
    {
        var palette = Palette();

        Assert.True(palette.ContainsKey("CouleurSurface"), "La couleur des panneaux est introuvable.");

        var fonds = new[] { "CouleurSurface", "CouleurSurfaceAlterne" };
        var textes = new[] { "CouleurTexte", "CouleurTexteSecondaire", "CouleurTexteAttenue" };

        var fautes = new List<string>();

        foreach (var texte in textes)
        {
            Assert.True(palette.ContainsKey(texte), $"{texte} est introuvable dans la palette.");

            foreach (var fond in fonds)
            {
                var rapport = Contraste(palette[texte], palette[fond]);

                if (rapport < SeuilTextePetit)
                {
                    fautes.Add(
                        $"  {texte} ({palette[texte]}) sur {fond} ({palette[fond]}) : " +
                        $"{rapport.ToString("0.00", CultureInfo.InvariantCulture)} pour 1");
                }
            }
        }

        Assert.True(fautes.Count == 0,
            $"Textes trop pâles pour être lus (minimum {SeuilTextePetit} pour 1) :" +
            Environment.NewLine + string.Join(Environment.NewLine, fautes));
    }

    /// <summary>
    /// La hiérarchie doit rester lisible : le texte principal plus marqué que
    /// le secondaire, lui-même plus marqué que l'atténué. Remonter le
    /// contraste de l'un sans regarder les autres écraserait la distinction
    /// qui les rend utiles.
    /// </summary>
    [Fact]
    public void Les_trois_niveaux_de_texte_restent_distincts()
    {
        var palette = Palette();
        var fond = palette["CouleurSurface"];

        var principal = Contraste(palette["CouleurTexte"], fond);
        var secondaire = Contraste(palette["CouleurTexteSecondaire"], fond);
        var attenue = Contraste(palette["CouleurTexteAttenue"], fond);

        Assert.True(principal > secondaire,
            "Le texte principal ne se détache plus du secondaire.");

        Assert.True(secondaire > attenue,
            "Le texte secondaire ne se détache plus de l'atténué.");
    }
}
