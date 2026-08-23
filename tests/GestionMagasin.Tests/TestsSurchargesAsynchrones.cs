using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Reproduit le montage de surcharges employé par les vues-modèles, et
/// vérifie que la variante retournant une valeur délègue bien à celle qui
/// travaille, au lieu de s'appeler elle-même.
///
/// Ce défaut a fermé la caisse à chaque encaissement pendant des jours. Il
/// est invisible : le code compile, aucun avertissement n'est émis, et la
/// pile d'appels sature en silence — Windows arrête alors le processus sans
/// exception, sans journal, sans rien à intercepter.
///
/// La classe testée est une copie fidèle du montage, car l'originale vit
/// dans le projet d'interface, qui ne s'exécute que sous Windows.
/// </summary>
public class TestsSurchargesAsynchrones
{
    private sealed class Socle
    {
        public int AppelsSurchargeGenerique { get; private set; }

        public int AppelsSurchargeTravail { get; private set; }

        public async Task<bool> ExecuterAsync(Func<Task> operation)
        {
            AppelsSurchargeTravail++;

            await operation().ConfigureAwait(false);

            return true;
        }

        public async Task<T?> ExecuterAsync<T>(Func<Task<T>> operation)
        {
            AppelsSurchargeGenerique++;

            // Une récursion doit être arrêtée ici : sans ce garde-fou, le
            // test ferait tomber tout l'exécuteur, comme il faisait tomber
            // le logiciel.
            Assert.True(AppelsSurchargeGenerique < 5,
                "La surcharge générique s'appelle elle-même : le corps de la lambda " +
                "doit être un bloc, sans quoi l'affectation produit une valeur et " +
                "le compilateur retient cette même surcharge.");

            T? resultat = default;

            await ExecuterAsync(
                async () => { resultat = await operation().ConfigureAwait(false); })
                .ConfigureAwait(false);

            return resultat;
        }
    }

    [Fact]
    public async Task La_variante_retournant_une_valeur_delegue_sans_se_rappeler()
    {
        var socle = new Socle();

        var valeur = await socle.ExecuterAsync(async () =>
        {
            await Task.Yield();
            return 1400m;
        });

        Assert.Equal(1400m, valeur);

        // Une seule entrée dans chaque surcharge : aucune récursion.
        Assert.Equal(1, socle.AppelsSurchargeGenerique);
        Assert.Equal(1, socle.AppelsSurchargeTravail);
    }

    [Fact]
    public async Task La_valeur_produite_par_l_operation_est_bien_restituee()
    {
        var socle = new Socle();

        // C'est ainsi que la caisse récupère la vente enregistrée : si la
        // valeur se perdait, le ticket ne pourrait pas être imprimé.
        var vente = await socle.ExecuterAsync(async () =>
        {
            await Task.Yield();
            return "VTE-000042";
        });

        Assert.Equal("VTE-000042", vente);
    }

    [Fact]
    public async Task Une_operation_sans_valeur_emprunte_la_surcharge_de_travail()
    {
        var socle = new Socle();
        var execute = false;

        await socle.ExecuterAsync(async () =>
        {
            await Task.Yield();
            execute = true;
        });

        Assert.True(execute);
        Assert.Equal(0, socle.AppelsSurchargeGenerique);
        Assert.Equal(1, socle.AppelsSurchargeTravail);
    }
}
