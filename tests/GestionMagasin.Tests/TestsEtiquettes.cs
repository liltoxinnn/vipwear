using GestionMagasin.Application.Impression;
using GestionMagasin.Domain.Common;
using Xunit;
using ZXing;
using ZXing.Common;

namespace GestionMagasin.Tests;

/// <summary>
/// Étiquettes code-barres des vêtements.
///
/// Le texte ne peut être dessiné que sous Windows. Les barres, elles, sont
/// entièrement calculées ici : elles sont imprimées point par point, telles
/// quelles, puis relues comme le ferait la douchette de la caisse.
/// </summary>
public class TestsEtiquettes
{
    private static readonly ReglagesEtiquettes Reglages40x30 = new();

    private static ArticleEtiquette Article(string code) =>
        new("t shirt nike", CompositionEtiquette.Detail("M", "Noir"), 2_500m, code);

    /// <summary>Imprime les barres dans une image en niveaux de gris et la fait lire.</summary>
    private static string? Lire(MiseEnPageEtiquette etiquette, BarcodeFormat format)
    {
        var image = new byte[etiquette.Largeur * etiquette.Hauteur];
        Array.Fill(image, (byte)255);

        foreach (var barre in etiquette.Barres)
        {
            for (var y = barre.Y; y < barre.Y + barre.Hauteur; y++)
            {
                for (var x = barre.X; x < barre.X + barre.Largeur; x++)
                {
                    image[y * etiquette.Largeur + x] = 0;
                }
            }
        }

        var lecteur = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { PossibleFormats = [format], TryHarder = true }
        };

        return lecteur.Decode(new RGBLuminanceSource(
            image, etiquette.Largeur, etiquette.Hauteur, RGBLuminanceSource.BitmapFormat.Gray8))?.Text;
    }

    [Theory]
    [InlineData(203)]
    [InlineData(300)]
    public void Le_code_du_magasin_se_relit_sur_une_etiquette_de_40_x_30(int resolution)
    {
        var code = CodesBarres.ComposerCodeMagasin(1234);

        var etiquette = CompositionEtiquette.Composer(Article(code), "VIP MEN’S STORE", "DA", Reglages40x30, resolution);

        Assert.Equal(FormatCodeBarres.Ean13, etiquette.Format);
        Assert.Equal(code, Lire(etiquette, BarcodeFormat.EAN_13));
    }

    [Fact]
    public void Les_barres_ont_une_largeur_entiere_d_au_moins_deux_points()
    {
        var etiquette = CompositionEtiquette.Composer(
            Article(CodesBarres.ComposerCodeMagasin(1)), "VIP MEN’S STORE", "DA", Reglages40x30);

        // Sur une imprimante à 203 points par pouce, deux points font
        // 0,25 mm : la barre la plus fine qu'une douchette lit sans peine.
        Assert.Equal(2, etiquette.PointsParModule);
        Assert.All(etiquette.Barres, b => Assert.Equal(0, b.Largeur % etiquette.PointsParModule));
        Assert.Equal(30, etiquette.Barres.Count);
    }

    [Theory]
    [InlineData("5901234123457")]
    [InlineData("6130000000121")]
    public void Le_code_d_origine_d_un_article_de_marque_se_relit_aussi(string code)
    {
        var etiquette = CompositionEtiquette.Composer(Article(code), null, "DA", Reglages40x30);

        Assert.Equal(code, Lire(etiquette, BarcodeFormat.EAN_13));
    }

    [Theory]
    [InlineData("600ABC123")]
    [InlineData("TSH-NOIR-M")]
    [InlineData("123456")]
    public void Un_code_saisi_a_la_main_est_imprime_en_code_128(string code)
    {
        var etiquette = CompositionEtiquette.Composer(Article(code), null, "DA", Reglages40x30);

        Assert.Equal(FormatCodeBarres.Code128, etiquette.Format);
        Assert.Equal(code, Lire(etiquette, BarcodeFormat.CODE_128));
    }

    [Theory]
    [InlineData(40, 30)]
    [InlineData(50, 25)]
    [InlineData(30, 20)]
    [InlineData(60, 40)]
    public void Rien_ne_deborde_et_le_texte_ne_touche_pas_les_barres(int largeur, int hauteur)
    {
        var reglages = new ReglagesEtiquettes { LargeurMm = largeur, HauteurMm = hauteur };
        var code = CodesBarres.ComposerCodeMagasin(98765);

        var etiquette = CompositionEtiquette.Composer(Article(code), "VIP MEN’S STORE", "DA", reglages);

        Assert.All(etiquette.Barres, b =>
        {
            Assert.InRange(b.X, 0, etiquette.Largeur - b.Largeur);
            Assert.InRange(b.Y, 0, etiquette.Hauteur - b.Hauteur);
        });

        var hautBarres = etiquette.Barres.Min(b => b.Y);
        var lignes = etiquette.Textes.Where(t => t.Haut < hautBarres).ToList();

        Assert.All(lignes, t => Assert.True(t.Haut + t.Corps * 1.2 <= hautBarres, t.Texte));
        Assert.Contains(lignes, t => t.Texte == "t shirt nike");
        Assert.Contains(lignes, t => t.Texte == "2 500 DA");

        Assert.Equal(code, Lire(etiquette, BarcodeFormat.EAN_13));
    }

    [Fact]
    public void Le_nom_du_magasin_s_efface_d_abord_sur_une_petite_etiquette()
    {
        var petite = CompositionEtiquette.Composer(
            Article(CodesBarres.ComposerCodeMagasin(5)),
            "VIP MEN’S STORE",
            "DA",
            new ReglagesEtiquettes { LargeurMm = 30, HauteurMm = 20 });

        var grande = CompositionEtiquette.Composer(
            Article(CodesBarres.ComposerCodeMagasin(5)), "VIP MEN’S STORE", "DA", Reglages40x30);

        Assert.DoesNotContain(petite.Textes, t => t.Texte == "VIP MEN’S STORE");
        Assert.Contains(grande.Textes, t => t.Texte == "VIP MEN’S STORE");
        Assert.Contains(petite.Textes, t => t.Texte == "2 500 DA");
    }

    [Fact]
    public void Le_prix_et_le_magasin_peuvent_etre_retires()
    {
        var reglages = Reglages40x30 with { AfficherPrix = false, AfficherMagasin = false };

        var etiquette = CompositionEtiquette.Composer(
            Article(CodesBarres.ComposerCodeMagasin(5)), "VIP MEN’S STORE", "DA", reglages);

        Assert.DoesNotContain(etiquette.Textes, t => t.Texte.Contains("DA") || t.Texte.Contains("STORE"));
    }

    [Theory]
    [InlineData(2_500, "2 500 DA")]
    [InlineData(950, "950 DA")]
    [InlineData(1_250.5, "1 250,50 DA")]
    public void Le_prix_s_affiche_sans_centimes_inutiles(decimal prix, string attendu)
    {
        Assert.Equal(attendu, CompositionEtiquette.FormaterPrix(prix, "DA"));
    }

    [Fact]
    public void Un_article_sans_code_ou_avec_des_accents_est_explique_en_francais()
    {
        var sansCode = Assert.Throws<EtiquetteImpossibleException>(() =>
            CompositionEtiquette.Composer(Article(""), null, "DA", Reglages40x30));

        var accents = Assert.Throws<EtiquetteImpossibleException>(() =>
            CompositionEtiquette.Composer(Article("ÉTÉ-Écru"), null, "DA", Reglages40x30));

        Assert.Contains("pas de code-barres", sansCode.Message);
        Assert.Contains("accents", accents.Message);
    }

    [Fact]
    public void Un_code_trop_long_pour_l_etiquette_est_explique()
    {
        var erreur = Assert.Throws<EtiquetteImpossibleException>(() =>
            CompositionEtiquette.Composer(
                Article("UNE-REFERENCE-BEAUCOUP-TROP-LONGUE-POUR-UNE-ETIQUETTE"),
                null,
                "DA",
                new ReglagesEtiquettes { LargeurMm = 25, HauteurMm = 20 }));

        Assert.Contains("trop long", erreur.Message);
    }

    [Fact]
    public void Les_reglages_abimes_reviennent_aux_valeurs_d_origine()
    {
        Assert.Equal(new ReglagesEtiquettes(), ReglagesEtiquettes.DepuisJson("{ pas du json"));

        var bornes = new ReglagesEtiquettes { LargeurMm = 5, HauteurMm = 500, NomImprimante = "  XP-365B " }.Normaliser();

        Assert.Equal(ReglagesEtiquettes.DimensionMinimale, bornes.LargeurMm);
        Assert.Equal(ReglagesEtiquettes.DimensionMaximale, bornes.HauteurMm);
        Assert.Equal("XP-365B", bornes.NomImprimante);
        Assert.Equal(bornes, ReglagesEtiquettes.DepuisJson(bornes.VersJson()));
    }
}
