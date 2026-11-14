using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Common;
using GestionMagasin.Infrastructure.Data;
using GestionMagasin.Tests.Socle;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Règles des codes-barres du magasin : un EAN-13 qui commence par « 20 »,
/// plage réservée par GS1 à l'usage interne, avec sa clé de contrôle.
/// </summary>
public class TestsReglesCodesBarres
{
    [Theory]
    [InlineData(1, "2000000000015")]
    [InlineData(42, "2000000000428")]
    [InlineData(9_999_999_999, "2099999999998")]
    public void Le_code_du_magasin_porte_le_prefixe_20_et_sa_cle(long numero, string attendu)
    {
        Assert.Equal(attendu, CodesBarres.ComposerCodeMagasin(numero));
        Assert.True(CodesBarres.EstEan13(attendu));
        Assert.True(CodesBarres.EstCodeMagasin(attendu));
    }

    [Theory]
    [InlineData("4006381333931")]
    [InlineData("5901234123457")]
    [InlineData("6130000000121")]
    public void Un_code_du_commerce_est_un_ean13_mais_pas_un_code_du_magasin(string code)
    {
        Assert.True(CodesBarres.EstEan13(code));
        Assert.False(CodesBarres.EstCodeMagasin(code));
    }

    [Theory]
    [InlineData("2000000000016")]
    [InlineData("200000000001")]
    [InlineData("20000000000155")]
    [InlineData("20000000000A5")]
    [InlineData("")]
    [InlineData(null)]
    public void Une_cle_fausse_ou_un_format_inattendu_n_est_pas_un_ean13(string? code)
    {
        Assert.False(CodesBarres.EstEan13(code));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10_000_000_000)]
    public void Un_numero_hors_plage_est_refuse(long numero)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CodesBarres.ComposerCodeMagasin(numero));
    }
}

/// <summary>
/// Attribution automatique des codes-barres : tout article reçoit un code
/// scannable dès sa création, et ceux créés avant l'ont au démarrage.
/// </summary>
public class TestsAttributionCodesBarres : BaseDeTest
{
    private IServiceProduits Produits => Service<IServiceProduits>();

    private async Task<(ProduitDto Produit, VarianteDto Variante)> ArticleSansCodeAsync(string taille = "M") =>
        await ConstructeurDonnees.CreerArticleAsync(Produits, codeBarres: " ", taille: taille);

    [Fact]
    public async Task Un_article_cree_sans_code_recoit_un_code_du_magasin_scannable()
    {
        var (_, variante) = await ArticleSansCodeAsync();

        Assert.True(CodesBarres.EstCodeMagasin(variante.CodeBarres), variante.CodeBarres);

        var scannee = await Produits.ObtenirVarianteParCodeBarresAsync(variante.CodeBarres!);

        Assert.NotNull(scannee);
        Assert.Equal(variante.Id, scannee.Id);
    }

    [Fact]
    public async Task Scanner_l_etiquette_dans_les_recherches_retrouve_l_article()
    {
        var (produit, variante) = await ArticleSansCodeAsync();
        var code = variante.CodeBarres!;

        var produits = await Produits.RechercherProduitsAsync(code);
        var stock = await Service<IServiceStock>().ListerStockAsync(code);
        var caisse = await Produits.RechercherVariantesAsync(code);

        Assert.Equal(produit.Id, Assert.Single(produits).Id);
        Assert.Equal(variante.Id, Assert.Single(stock).VarianteProduitId);
        Assert.Equal(variante.Id, Assert.Single(caisse).Id);
    }

    [Fact]
    public async Task Le_code_d_origine_saisi_a_la_main_est_conserve()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, codeBarres: "6130000000123");

        Assert.Equal("6130000000123", variante.CodeBarres);
    }

    [Fact]
    public async Task Les_declinaisons_generees_ont_chacune_leur_propre_code()
    {
        var (produit, _) = await ArticleSansCodeAsync();

        var tailles = await Produits.ListerTaillesAsync(systemeTailleId: produit.SystemeTailleId);
        var couleurs = await Produits.ListerCouleursAsync();

        await Produits.GenererVariantesAsync(
            produit.Id,
            tailles.Where(t => t.Nom is "S" or "L" or "XL").Select(t => t.Id).ToList(),
            couleurs.Where(c => c.Nom is "Blanc" or "Bleu").Select(c => c.Id).ToList());

        var fiche = await Produits.ObtenirProduitAsync(produit.Id);
        var codes = fiche!.Variantes.Select(v => v.CodeBarres).ToList();

        Assert.Equal(7, codes.Count);
        Assert.All(codes, code => Assert.True(CodesBarres.EstCodeMagasin(code), code));
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public async Task Vider_le_champ_garde_le_code_du_magasin_deja_imprime()
    {
        var (_, variante) = await ArticleSansCodeAsync();

        var modifiee = await Produits.ModifierVarianteAsync(variante.Id, Demande(variante, codeBarres: ""));

        Assert.Equal(variante.CodeBarres, modifiee.CodeBarres);
    }

    [Fact]
    public async Task Vider_le_code_d_origine_le_remplace_par_un_code_du_magasin()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, codeBarres: "6130000000777");

        var modifiee = await Produits.ModifierVarianteAsync(variante.Id, Demande(variante, codeBarres: null));

        Assert.True(CodesBarres.EstCodeMagasin(modifiee.CodeBarres), modifiee.CodeBarres);
    }

    [Fact]
    public async Task Un_code_du_magasin_deja_saisi_a_la_main_est_saute()
    {
        var (_, premier) = await ArticleSansCodeAsync("S");
        var numero = long.Parse(premier.CodeBarres![2..12]);

        // Quelqu'un a saisi à la main, sur un autre article, le code que la
        // séquence allait donner ensuite.
        var suivant = CodesBarres.ComposerCodeMagasin(numero + 1);
        await ConstructeurDonnees.CreerArticleAsync(Produits, codeBarres: suivant, taille: "L");

        var (_, troisieme) = await ArticleSansCodeAsync("XL");

        Assert.Equal(CodesBarres.ComposerCodeMagasin(numero + 2), troisieme.CodeBarres);
    }

    [Fact]
    public async Task Les_articles_crees_avant_recoivent_un_code_au_demarrage()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, codeBarres: "6130000000555");

        var contexte = Service<ContexteMagasin>();
        await contexte.Database.ExecuteSqlRawAsync(
            "UPDATE variantes_produits SET code_barres = NULL WHERE id = {0}", variante.Id);

        await Service<InitialiseurBaseDonnees>().AmorcerAsync();

        var apres = await Produits.ObtenirProduitAsync(variante.ProduitId);
        var code = apres!.Variantes.Single(v => v.Id == variante.Id).CodeBarres;

        Assert.True(CodesBarres.EstCodeMagasin(code), code);
        Assert.NotNull(await Produits.ObtenirVarianteParCodeBarresAsync(code!));
    }

    private static DemandeVariante Demande(VarianteDto variante, string? codeBarres) => new()
    {
        TailleId = variante.TailleId,
        CouleurId = variante.CouleurId,
        Sku = variante.Sku,
        CodeBarres = codeBarres,
        PrixAchat = variante.PrixAchatSpecifique,
        PrixVente = variante.PrixVenteSpecifique,
        SeuilMinimum = variante.SeuilMinimum
    };
}
