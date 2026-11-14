using GestionMagasin.Application.Impression;
using GestionMagasin.Domain.Common;
using Xunit;
using ZXing;
using ZXing.Common;

namespace GestionMagasin.Tests;

/// <summary>
/// Étiquettes sur une imprimante de tickets (POS-58 R58P) garnie d'un
/// rouleau d'étiquettes.
///
/// Elle ne voit pas les étiquettes : la série part en une seule image, chaque
/// étiquette exactement un pas plus bas que la précédente, et chaque travail
/// avance le papier d'un nombre entier de pas pour que la série suivante
/// retombe juste.
/// </summary>
public class TestsEtiquettesSurTickets
{
    private static readonly ReglagesEtiquettes R58P = new()
    {
        NomImprimante = "POS-58 R58P",
        LargeurMm = 40,
        HauteurMm = 20,
        EspaceMm = 2,
        DecalageHorizontalMm = 0,
        DecalageVerticalMm = 11
    };

    /// <summary>Étiquette toute noire, pour repérer exactement où elle tombe.</summary>
    private static ImageEtiquette Noire(int nombre) =>
        new(Enumerable.Repeat((byte)0, 320 * 160).ToArray(), 320, 160, nombre);

    [Theory]
    [InlineData("POS-58 R58P", TypeImprimanteEtiquettes.Tickets58)]
    [InlineData("POS58 Printer", TypeImprimanteEtiquettes.Tickets58)]
    [InlineData("XP-80C", TypeImprimanteEtiquettes.Tickets80)]
    [InlineData("Xprinter XP-365B", TypeImprimanteEtiquettes.Etiquettes)]
    [InlineData("ZDesigner GK420d", TypeImprimanteEtiquettes.Etiquettes)]
    [InlineData("Microsoft Print to PDF", TypeImprimanteEtiquettes.Etiquettes)]
    public void La_sorte_d_imprimante_se_devine_a_son_nom(string nom, TypeImprimanteEtiquettes attendu)
    {
        Assert.Equal(attendu, ReglagesEtiquettes.Deviner(nom));
        Assert.Equal(attendu, new ReglagesEtiquettes { NomImprimante = nom }.TypeEffectif);
    }

    [Fact]
    public void La_sorte_choisie_a_la_main_l_emporte_sur_le_nom()
    {
        var reglages = R58P with { Type = TypeImprimanteEtiquettes.Etiquettes };

        Assert.False(reglages.SurImprimanteDeTickets);
    }

    [Fact]
    public void Le_pas_est_la_hauteur_plus_l_espace_au_point_pres()
    {
        // 22 mm à 8 points par millimètre.
        Assert.Equal(176, BandeEtiquettes.Pas(R58P));
        Assert.Equal(180, BandeEtiquettes.Pas(R58P with { EspaceMm = 2.5 }));
    }

    [Fact]
    public void Chaque_etiquette_tombe_exactement_un_pas_plus_bas()
    {
        var (gris, largeur, hauteur) = BandeEtiquettes.Assembler([Noire(2), Noire(1)], R58P);

        Assert.Equal(384, largeur);

        // Trois étiquettes, plus le pas qui fait sortir la dernière.
        Assert.Equal(4 * 176, hauteur);

        var debut = (int)Math.Round(11 * 203 / 25.4);
        var gauche = (384 - 320) / 2;

        for (var i = 0; i < 3; i++)
        {
            var haut = debut + i * 176;

            Assert.Equal(255, gris[(haut - 1) * largeur + gauche]);
            Assert.Equal(0, gris[haut * largeur + gauche]);
            Assert.Equal(0, gris[(haut + 159) * largeur + gauche + 319]);
            Assert.Equal(255, gris[(haut + 160) * largeur + gauche]);
            Assert.Equal(255, gris[haut * largeur + gauche - 1]);
            Assert.Equal(255, gris[haut * largeur + gauche + 320]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5.5)]
    [InlineData(11)]
    [InlineData(21.9)]
    [InlineData(30)]
    public void Un_travail_avance_toujours_d_un_nombre_entier_de_pas(double decalageVertical)
    {
        var reglages = R58P with { DecalageVerticalMm = decalageVertical };

        var (_, _, hauteur) = BandeEtiquettes.Assembler([Noire(3)], reglages);

        Assert.Equal(0, hauteur % BandeEtiquettes.Pas(reglages));
    }

    [Fact]
    public void Le_decalage_horizontal_deplace_l_impression_et_coupe_ce_qui_depasse()
    {
        var (gris, largeur, _) = BandeEtiquettes.Assembler([Noire(1)], R58P with { DecalageHorizontalMm = 3 });
        var haut = (int)Math.Round(11 * 203 / 25.4);
        var gauche = (384 - 320) / 2 + 24;

        Assert.Equal(255, gris[haut * largeur + gauche - 1]);
        Assert.Equal(0, gris[haut * largeur + gauche]);

        // Poussée de 20 mm, l'étiquette sort de la tête : le bord est perdu,
        // sans erreur ni débordement sur la rangée suivante.
        var (coupee, _, _) = BandeEtiquettes.Assembler([Noire(1)], R58P with { DecalageHorizontalMm = 20 });

        Assert.Equal(0, coupee[haut * largeur + 383]);
        Assert.Equal(255, coupee[(haut + 1) * largeur + 0]);
    }

    [Fact]
    public void Le_code_barres_se_relit_dans_la_bande_envoyee_a_l_imprimante()
    {
        var code = CodesBarres.ComposerCodeMagasin(1227);
        var mise = CompositionEtiquette.Composer(
            new ArticleEtiquette("bas survette g taille", "", 2_900m, code), "VIP MEN’S STORE", "DA", R58P);

        var image = new byte[mise.Largeur * mise.Hauteur];
        Array.Fill(image, (byte)255);

        foreach (var barre in mise.Barres)
        {
            for (var y = barre.Y; y < barre.Y + barre.Hauteur; y++)
            {
                for (var x = barre.X; x < barre.X + barre.Largeur; x++)
                {
                    image[y * mise.Largeur + x] = 0;
                }
            }
        }

        var (gris, largeur, hauteur) = BandeEtiquettes.Assembler(
            [new ImageEtiquette(image, mise.Largeur, mise.Hauteur, 2)], R58P);

        // L'image passe par la même conversion que pour les tickets.
        var points = Monochrome.VersPoints(gris, largeur, hauteur);
        var pas = Monochrome.OctetsParRangee(largeur);
        var relu = new byte[largeur * hauteur];

        for (var y = 0; y < hauteur; y++)
        {
            for (var x = 0; x < largeur; x++)
            {
                relu[y * largeur + x] = (points[y * pas + x / 8] & (0x80 >> (x % 8))) != 0 ? (byte)0 : (byte)255;
            }
        }

        var lecteur = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.EAN_13], TryHarder = true }
        };

        Assert.Equal(code, lecteur.Decode(new RGBLuminanceSource(
            relu, largeur, hauteur, RGBLuminanceSource.BitmapFormat.Gray8))?.Text);
    }

    [Fact]
    public void La_serie_part_en_image_seule_sans_coupe_ni_avance_en_lignes()
    {
        var (gris, largeur, hauteur) = BandeEtiquettes.Assembler([Noire(1)], R58P);
        var points = Monochrome.VersPoints(gris, largeur, hauteur);

        var travail = EscPos.Etiquettes(points, largeur, hauteur);

        Assert.Equal([0x1B, 0x40], travail[..2]);

        // Un en-tête d'image par bande de 128 rangées, et rien d'autre.
        var bandes = (hauteur + EscPos.RangeesParBande - 1) / EscPos.RangeesParBande;
        Assert.Equal(2 + bandes * 8 + points.Length, travail.Length);
    }

    [Fact]
    public void L_etiquette_de_reglage_dessine_un_cadre_aux_bords_exacts()
    {
        var reglage = CompositionEtiquette.Reglage(R58P, 3, BandeEtiquettes.Resolution);

        Assert.Equal(320, reglage.Largeur);
        Assert.Equal(160, reglage.Hauteur);
        Assert.Contains(reglage.Barres, b => b is { X: 0, Y: 0, Largeur: 320 });
        Assert.Contains(reglage.Barres, b => b.X == 0 && b.Y + b.Hauteur == 160 && b.Largeur == 320);
        Assert.Contains(reglage.Barres, b => b is { X: 0, Y: 0, Hauteur: 160 });
        Assert.Contains(reglage.Barres, b => b.X + b.Largeur == 320 && b.Hauteur == 160);
        Assert.Contains(reglage.Textes, t => t.Texte == "RÉGLAGE 3");
        Assert.All(reglage.Barres, b =>
        {
            Assert.InRange(b.X, 0, 320 - b.Largeur);
            Assert.InRange(b.Y, 0, 160 - b.Hauteur);
        });
    }

    [Fact]
    public void Par_defaut_la_premiere_etiquette_commence_sans_aucun_blanc()
    {
        var reglages = new ReglagesEtiquettes { NomImprimante = "POS-58 R58P" };

        var (gris, largeur, _) = BandeEtiquettes.Assembler([Noire(1)], reglages);

        Assert.Equal(0, reglages.DecalageVerticalMm);
        Assert.Equal(0, gris[0 * largeur + (384 - 320) / 2]);
    }

    [Fact]
    public void L_ancienne_avance_de_11_mm_retenue_disparait()
    {
        var anciens = ReglagesEtiquettes.DepuisJson(
            """{ "Version": 2, "NomImprimante": "POS-58 R58P", "LargeurMm": 40, "HauteurMm": 20, "DecalageVerticalMm": 11 }""");

        Assert.Equal(0, anciens.DecalageVerticalMm);

        // Une avance réglée depuis, même de 11 mm, est gardée.
        var reglee = R58P with { DecalageVerticalMm = 11 };
        Assert.Equal(11, ReglagesEtiquettes.DepuisJson(reglee.VersJson()).DecalageVerticalMm);
    }

    [Fact]
    public void Les_reglages_de_position_sont_retenus_et_bornes()
    {
        var reglages = R58P with
        {
            Type = TypeImprimanteEtiquettes.Tickets58,
            EspaceMm = 2.46,
            DecalageHorizontalMm = -3.5,
            DecalageVerticalMm = 99,
            AfficherDetail = true
        };

        var relus = ReglagesEtiquettes.DepuisJson(reglages.VersJson());

        Assert.Equal(TypeImprimanteEtiquettes.Tickets58, relus.Type);
        Assert.Equal(2.5, relus.EspaceMm);
        Assert.Equal(-3.5, relus.DecalageHorizontalMm);
        Assert.Equal(40, relus.DecalageVerticalMm);
        Assert.True(relus.AfficherDetail);

        // Un fichier d'avant ces réglages prend les valeurs d'origine.
        var anciens = ReglagesEtiquettes.DepuisJson("""{ "NomImprimante": "POS-58 R58P", "LargeurMm": 40, "HauteurMm": 20 }""");

        Assert.Null(anciens.Type);
        Assert.Equal(TypeImprimanteEtiquettes.Tickets58, anciens.TypeEffectif);
        Assert.Equal(ReglagesEtiquettes.EspaceParDefaut, anciens.EspaceMm);
        Assert.False(anciens.AfficherDetail);
    }
}
