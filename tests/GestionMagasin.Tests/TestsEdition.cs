using GestionMagasin.Application.Common;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Repère de version affiché dans le menu.
///
/// Il répond à la question qui revient à chaque livraison : « est-ce bien la
/// nouvelle version qui tourne ? » Le numéro seul n'y suffit pas — il reste à
/// 1.0.0 d'une livraison à l'autre — c'est l'horodatage de compilation qui
/// tranche.
/// </summary>
public class TestsEdition
{
    [Fact]
    public void La_version_et_l_horodatage_sont_mis_en_forme()
    {
        Assert.Equal("v1.0.0 — 29/08 03:25", Edition.Decrire("1.0.0+20260829-0325"));
    }

    [Fact]
    public void Une_version_sans_horodatage_reste_lisible()
    {
        Assert.Equal("v1.0.0", Edition.Decrire("1.0.0"));
    }

    /// <summary>
    /// Un horodatage d'une forme inattendue vaut mieux affiché que tu : il
    /// reste un repère, même illisible.
    /// </summary>
    [Fact]
    public void Un_horodatage_inattendu_est_montre_tel_quel()
    {
        Assert.Equal("v1.0.0 — abcdef", Edition.Decrire("1.0.0+abcdef"));
    }

    [Fact]
    public void Une_version_absente_n_affiche_rien()
    {
        Assert.Equal(string.Empty, Edition.Decrire(null));
        Assert.Equal(string.Empty, Edition.Decrire("   "));
    }

    /// <summary>
    /// Le fichier projet doit horodater la compilation, faute de quoi le
    /// menu afficherait le même repère pour toutes les livraisons.
    /// </summary>
    [Fact]
    public void Le_projet_horodate_chaque_compilation()
    {
        var dossier = new DirectoryInfo(AppContext.BaseDirectory);

        while (dossier is not null
               && !Directory.Exists(Path.Combine(dossier.FullName, "src", "GestionMagasin.App")))
        {
            dossier = dossier.Parent;
        }

        Assert.NotNull(dossier);

        var projet = File.ReadAllText(Path.Combine(
            dossier!.FullName, "src", "GestionMagasin.App", "GestionMagasin.App.csproj"));

        Assert.Contains("<SourceRevisionId>", projet, StringComparison.Ordinal);
    }
}
