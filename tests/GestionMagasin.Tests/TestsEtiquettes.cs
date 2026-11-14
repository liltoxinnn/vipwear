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
    /// <summary>Les étiquettes du magasin : 40 × 20 mm.</summary>
    private static readonly ReglagesEtiquettes ReglagesDuMagasin = new();

    private static readonly ReglagesEtiquettes Reglages40x30 = new() { LargeurMm = 40, HauteurMm = 30 };

    private const double PointsParMm = 203 / 25.4;

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

    [Fact]
    public void Les_etiquettes_du_magasin_font_40_x_20_mm()
    {
        Assert.Equal(40, ReglagesDuMagasin.LargeurMm);
        Assert.Equal(20, ReglagesDuMagasin.HauteurMm);
    }

    [Theory]
    [InlineData(20, 203)]
    [InlineData(20, 300)]
    [InlineData(30, 203)]
    [InlineData(30, 300)]
    public void Le_code_du_magasin_se_relit_sur_une_etiquette_de_40_mm(int hauteur, int resolution)
    {
        var code = CodesBarres.ComposerCodeMagasin(1234);
        var reglages = new ReglagesEtiquettes { LargeurMm = 40, HauteurMm = hauteur };

        var etiquette = CompositionEtiquette.Composer(Article(code), "VIP MEN’S STORE", "DA", reglages, resolution);

        Assert.Equal(FormatCodeBarres.Ean13, etiquette.Format);
        Assert.Equal(code, Lire(etiquette, BarcodeFormat.EAN_13));
    }

    [Fact]
    public void Sur_40_x_20_la_taille_la_couleur_et_le_prix_partagent_une_ligne()
    {
        var etiquette = CompositionEtiquette.Composer(
            Article(CodesBarres.ComposerCodeMagasin(7)), "VIP MEN’S STORE", "DA", ReglagesDuMagasin);

        var detail = etiquette.Textes.Single(t => t.Texte == "Taille M — Noir");
        var prix = etiquette.Textes.Single(t => t.Texte == "2 500 DA");

        Assert.Equal(AlignementTexte.Gauche, detail.Alignement);
        Assert.Equal(AlignementTexte.Droite, prix.Alignement);
        Assert.True(detail.X + detail.Largeur <= prix.X, "Le prix empiète sur la taille et la couleur.");

        // Même ligne de pied : le bas des deux textes coïncide.
        Assert.Equal(detail.Haut + (int)Math.Ceiling(detail.Corps * 1.2), prix.Haut + (int)Math.Ceiling(prix.Corps * 1.2));

        // Et c'est ce qui laisse au code-barres la hauteur qu'il lui faut.
        var hauteurBarres = etiquette.Barres.Min(b => b.Hauteur);
        Assert.True(hauteurBarres >= 7 * PointsParMm - 1, $"Barres de {hauteurBarres / PointsParMm:N1} mm seulement.");

        Assert.Contains(etiquette.Textes, t => t.Texte == "t shirt nike");
    }

    [Fact]
    public void Sur_une_etiquette_haute_le_prix_a_sa_propre_ligne_en_grand()
    {
        var etiquette = CompositionEtiquette.Composer(
            Article(CodesBarres.ComposerCodeMagasin(7)), "VIP MEN’S STORE", "DA", Reglages40x30);

        var detail = etiquette.Textes.Single(t => t.Texte == "Taille M — Noir");
        var prix = etiquette.Textes.Single(t => t.Texte == "2 500 DA");

        Assert.True(prix.Haut > detail.Haut, "Le prix devrait être sous la taille et la couleur.");
        Assert.True(prix.Corps > detail.Corps);
        Assert.Contains(etiquette.Textes, t => t.Texte == "VIP MEN’S STORE");
    }

    [Fact]
    public void Retirer_le_nom_du_magasin_donne_un_texte_plus_grand_sur_40_x_20()
    {
        var avec = CompositionEtiquette.Composer(
            Article(CodesBarres.ComposerCodeMagasin(7)), "VIP MEN’S STORE", "DA", ReglagesDuMagasin);

        var sans = CompositionEtiquette.Composer(
            Article(CodesBarres.ComposerCodeMagasin(7)), "VIP MEN’S STORE", "DA",
            ReglagesDuMagasin with { AfficherMagasin = false });

        Assert.Contains(avec.Textes, t => t.Texte == "VIP MEN’S STORE");
        Assert.DoesNotContain(sans.Textes, t => t.Texte == "VIP MEN’S STORE");

        Assert.True(
            sans.Textes.Single(t => t.Texte == "t shirt nike").Corps >
            avec.Textes.Single(t => t.Texte == "t shirt nike").Corps);
    }

    [Fact]
    public void Les_barres_ont_une_largeur_entiere_d_au_moins_deux_points()
    {
        var etiquette = CompositionEtiquette.Composer(
            Article(CodesBarres.ComposerCodeMagasin(1)), "VIP MEN’S STORE", "DA", ReglagesDuMagasin);

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
        var etiquette = CompositionEtiquette.Composer(Article(code), null, "DA", ReglagesDuMagasin);

        Assert.Equal(code, Lire(etiquette, BarcodeFormat.EAN_13));
    }

    [Theory]
    [InlineData("600ABC123")]
    [InlineData("TSH-NOIR-M")]
    [InlineData("123456")]
    public void Un_code_saisi_a_la_main_est_imprime_en_code_128(string code)
    {
        var etiquette = CompositionEtiquette.Composer(Article(code), null, "DA", ReglagesDuMagasin);

        Assert.Equal(FormatCodeBarres.Code128, etiquette.Format);
        Assert.Equal(code, Lire(etiquette, BarcodeFormat.CODE_128));
    }

    [Theory]
    [InlineData(40, 20)]
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

        Assert.All(etiquette.Textes, t =>
        {
            Assert.InRange(t.X, 0, etiquette.Largeur - t.Largeur);
            Assert.InRange(t.Haut, 0, etiquette.Hauteur - (int)Math.Ceiling(t.Corps * 1.2));
        });

        var hautBarres = etiquette.Barres.Min(b => b.Y);
        var lignes = etiquette.Textes.Where(t => t.Haut < hautBarres).ToList();

        Assert.All(lignes, t => Assert.True(t.Haut + t.Corps * 1.2 <= hautBarres, t.Texte));
        Assert.Contains(lignes, t => t.Texte == "t shirt nike");
        Assert.Contains(lignes, t => t.Texte == "2 500 DA");

        Assert.Equal(code, Lire(etiquette, BarcodeFormat.EAN_13));
    }

    [Fact]
    public void Le_prix_et_le_magasin_peuvent_etre_retires()
    {
        var reglages = ReglagesDuMagasin with { AfficherPrix = false, AfficherMagasin = false };

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
            CompositionEtiquette.Composer(Article(""), null, "DA", ReglagesDuMagasin));

        var accents = Assert.Throws<EtiquetteImpossibleException>(() =>
            CompositionEtiquette.Composer(Article("ÉTÉ-Écru"), null, "DA", ReglagesDuMagasin));

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
    public void L_ancienne_taille_retenue_de_40_x_30_devient_40_x_20()
    {
        // Fichier écrit par la première version, après une impression.
        var anciens = ReglagesEtiquettes.DepuisJson(
            """{ "NomImprimante": "Xprinter XP-365B", "LargeurMm": 40, "HauteurMm": 30, "AfficherPrix": true }""");

        Assert.Equal(40, anciens.LargeurMm);
        Assert.Equal(20, anciens.HauteurMm);
        Assert.Equal("Xprinter XP-365B", anciens.NomImprimante);
    }

    [Fact]
    public void Une_taille_choisie_depuis_est_gardee()
    {
        var choisis = new ReglagesEtiquettes { NomImprimante = "XP-365B", LargeurMm = 40, HauteurMm = 30 };
        var autre = ReglagesEtiquettes.DepuisJson("""{ "LargeurMm": 50, "HauteurMm": 30 }""");

        Assert.Equal(30, ReglagesEtiquettes.DepuisJson(choisis.VersJson()).HauteurMm);
        Assert.Equal(30, autre.HauteurMm);
    }

    [Theory]
    [InlineData(40, 20, true)]
    [InlineData(40.4, 19.6, true)]
    [InlineData(60, 40, false)]
    [InlineData(101.6, 152.4, false)]
    [InlineData(20, 40, false)]
    public void Le_papier_du_pilote_doit_avoir_la_taille_des_etiquettes(double largeur, double hauteur, bool convient)
    {
        Assert.Equal(convient, FormatPilote.Convient(largeur, hauteur, ReglagesDuMagasin));
    }

    [Fact]
    public void Un_pilote_sur_un_autre_papier_est_explique_avec_la_marche_a_suivre()
    {
        var message = FormatPilote.Expliquer("Xprinter XP-365B", ReglagesDuMagasin, 60, 40);

        Assert.Contains("60 × 40 mm", message);
        Assert.Contains("40 × 20 mm", message);
        Assert.Contains("Options d'impression", message);
        Assert.Contains("étiquettes avec espace", message);
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
