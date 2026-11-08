using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Impression;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Domain.Securite;
using GestionMagasin.Infrastructure.Data;
using GestionMagasin.Tests.Socle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZXing;
using ZXing.Common;

namespace GestionMagasin.Tests;

/// <summary>
/// QR code imprimé en bas du ticket.
///
/// Un QR code qu'un téléphone ne lit pas est pire qu'une absence de QR code :
/// le client essaie, échoue, et retient que le magasin s'est trompé. Le code
/// est donc relu ici par un vrai lecteur, après être passé par la même
/// conversion en noir et blanc que celle qui part à l'imprimante, à la taille
/// exacte où il sera imprimé.
/// </summary>
public class TestsCodeQrTicket : BaseDeTest
{
    private const string LienDuMagasin = "https://linktr.ee/VIPMENSSTORE";

    private static readonly ReglagesTicket Rouleau58 = new() { NomImprimante = "POS-58" };

    /// <summary>
    /// Imprime le code comme le fait le ticket — carrés de points entiers,
    /// zone de silence, conversion en points — puis le relit.
    /// </summary>
    private static string? ImprimerPuisLire(string contenu)
    {
        var matrice = CodeQr.Matrice(contenu);
        var cote = matrice.GetLength(0);
        var carre = CodeQr.PointsParCarre(cote);
        var silence = CodeQr.ZoneDeSilence * carre;
        var taille = cote * carre + 2 * silence;

        // Image en niveaux de gris, blanche, avec les carrés en noir.
        var gris = Enumerable.Repeat((byte)255, taille * taille).ToArray();

        for (var y = 0; y < cote; y++)
        {
            for (var x = 0; x < cote; x++)
            {
                if (!matrice[y, x])
                {
                    continue;
                }

                for (var dy = 0; dy < carre; dy++)
                {
                    for (var dx = 0; dx < carre; dx++)
                    {
                        gris[(silence + y * carre + dy) * taille + silence + x * carre + dx] = 0;
                    }
                }
            }
        }

        // Même conversion que pour l'imprimante, puis retour en pixels.
        var points = Monochrome.VersPoints(gris, taille, taille);
        var pas = Monochrome.OctetsParRangee(taille);
        var lu = new byte[taille * taille];

        for (var y = 0; y < taille; y++)
        {
            for (var x = 0; x < taille; x++)
            {
                var noir = (points[y * pas + x / 8] & (0x80 >> (x % 8))) != 0;
                lu[y * taille + x] = noir ? (byte)0 : (byte)255;
            }
        }

        var lecteur = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.QR_CODE], TryHarder = true }
        };

        return lecteur.Decode(new RGBLuminanceSource(lu, taille, taille, RGBLuminanceSource.BitmapFormat.Gray8))
            ?.Text;
    }

    // ==================================================================
    // Le code
    // ==================================================================

    [Fact]
    public void Le_code_du_magasin_se_relit_apres_conversion_pour_l_imprimante()
    {
        Assert.Equal(LienDuMagasin, ImprimerPuisLire(LienDuMagasin));
    }

    /// <summary>
    /// Le lien le plus long accepté donne le plus grand code, aux carrés les
    /// plus fins : c'est le cas limite de lisibilité.
    /// </summary>
    [Fact]
    public void Le_lien_le_plus_long_accepte_se_relit_encore()
    {
        var lien = "https://exemple.dz/" + new string('a', CodeQr.LongueurMaximale - 19);

        Assert.Equal(CodeQr.LongueurMaximale, lien.Length);
        Assert.Equal(lien, ImprimerPuisLire(lien));
    }

    [Fact]
    public void Le_code_du_magasin_est_petit()
    {
        var matrice = CodeQr.Matrice(LienDuMagasin);
        var cote = matrice.GetLength(0);
        var largeur = cote * CodeQr.PointsParCarre(cote);

        // 29 carrés de 4 points : 116 points, 14,5 mm sur un rouleau de 48 mm
        // imprimables — moins d'un tiers de la largeur.
        Assert.Equal(29, cote);
        Assert.Equal(116, largeur);
        Assert.True(largeur < Rouleau58.PointsParLigne / 3);
    }

    [Fact]
    public void Le_plus_grand_code_tient_encore_dans_le_papier()
    {
        var matrice = CodeQr.Matrice(new string('a', CodeQr.LongueurMaximale));
        var cote = matrice.GetLength(0);
        var largeur = (cote + 2 * CodeQr.ZoneDeSilence) * CodeQr.PointsParCarre(cote);

        Assert.True(largeur <= Rouleau58.PointsParLigne, $"{largeur} points pour {Rouleau58.PointsParLigne}");
    }

    /// <summary>
    /// Les trois coins d'un QR code portent chacun un carré de repérage : leur
    /// présence prouve que la marge a été retirée au bon endroit.
    /// </summary>
    [Fact]
    public void La_matrice_commence_sur_les_reperes_d_angle()
    {
        var matrice = CodeQr.Matrice(LienDuMagasin);
        var n = matrice.GetLength(0);

        for (var i = 0; i < 7; i++)
        {
            Assert.True(matrice[0, i], "repère haut gauche");
            Assert.True(matrice[0, n - 1 - i], "repère haut droit");
            Assert.True(matrice[n - 1, i], "repère bas gauche");
        }
    }

    // ==================================================================
    // Sur le ticket
    // ==================================================================

    private static VenteDto Vente() => new()
    {
        NumeroVente = "VTE-2026-000001",
        DateVente = DateTime.Now,
        SousTotal = 2_300m,
        Total = 2_300m,
        MontantPaye = 2_300m,
        Lignes = [new LigneVenteDto { Designation = "HOODIE — Rouge / S", Quantite = 1, PrixUnitaire = 2_300m, Total = 2_300m }]
    };

    [Fact]
    public void Le_ticket_se_termine_par_le_code_et_sa_legende()
    {
        var magasin = new ParametresMagasin { NomMagasin = "VIP MEN'S STORE", LienQrCodeTicket = $"  {LienDuMagasin}  " };

        var lignes = CompositionTicket.Composer(Vente(), magasin, Rouleau58);

        Assert.Equal(new LigneTicket(LienDuMagasin, StyleLigne.CodeQr), lignes[^2]);
        Assert.Equal(CompositionTicket.LegendeQrCode, lignes[^1].Texte.Trim());
        Assert.True(CompositionTicket.LegendeQrCode.Length <= Rouleau58.Colonnes,
            "La légende doit tenir sur une seule ligne de 58 mm.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sans_lien_le_ticket_n_a_pas_de_code(string? lien)
    {
        var magasin = new ParametresMagasin { NomMagasin = "VIP MEN'S STORE", LienQrCodeTicket = lien };

        var lignes = CompositionTicket.Composer(Vente(), magasin, Rouleau58);

        Assert.DoesNotContain(lignes, l => l.Style == StyleLigne.CodeQr);
        Assert.DoesNotContain(lignes, l => l.Texte.Contains(CompositionTicket.LegendeQrCode));
    }

    // ==================================================================
    // Le réglage
    // ==================================================================

    [Fact]
    public async Task Le_lien_est_enregistre_et_relu()
    {
        await ConnecterAsync(NomsRoles.Administrateur);
        var parametres = Service<IServiceParametres>();

        var fiche = await parametres.ObtenirAsync();
        fiche.LienQrCodeTicket = $"  {LienDuMagasin} ";

        await parametres.EnregistrerAsync(fiche);

        Assert.Equal(LienDuMagasin, await LireEnBaseAsync());
    }

    [Fact]
    public async Task Un_lien_trop_long_est_refuse()
    {
        await ConnecterAsync(NomsRoles.Administrateur);
        var parametres = Service<IServiceParametres>();

        var fiche = await parametres.ObtenirAsync();
        fiche.LienQrCodeTicket = new string('a', CodeQr.LongueurMaximale + 1);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() => parametres.EnregistrerAsync(fiche));

        Assert.Contains("QR code", erreur.Message);
    }

    /// <summary>Relit le lien en base, hors du cache des paramètres.</summary>
    private async Task<string?> LireEnBaseAsync()
    {
        using var portee = CreerPortee();
        var contexte = portee.ServiceProvider.GetRequiredService<ContexteMagasin>();

        var fiche = await contexte.Set<ParametresMagasin>().AsNoTracking().SingleAsync();

        return fiche.LienQrCodeTicket;
    }
}
