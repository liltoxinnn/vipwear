using GestionMagasin.ServeurEmbarque;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Verrou « postmaster.pid » laissé par un arrêt brutal.
///
/// C'est la panne qui bloquait le logiciel au démarrage : le verrou d'un
/// serveur mort paraissait valide parce qu'un autre programme avait hérité
/// de son numéro de processus. Le magasin voyait « Le serveur de base de
/// données n'a pas pu démarrer » à chaque lancement, sans recours.
/// </summary>
public class TestsVerrouGrappe
{
    private const string Donnees = @"C:\Users\magasin\AppData\Local\GestionMagasin\donnees";

    private static string[] Verrou(int identifiant, string? dossier = null) =>
        [identifiant.ToString(), dossier ?? Donnees, "1756000000", "5433", ""];

    /// <summary>
    /// Le cœur du défaut. Windows réattribue les numéros de processus : après
    /// un redémarrage, celui du serveur mort désigne souvent un navigateur.
    /// Se contenter de constater qu'un processus existe gardait le verrou, et
    /// le serveur ne redémarrait plus jamais.
    /// </summary>
    [Fact]
    public void Un_numero_repris_par_un_autre_programme_ne_protege_rien()
    {
        Assert.True(VerrouGrappe.EstPerime(Verrou(4242), Donnees, _ => "chrome"));
        Assert.True(VerrouGrappe.EstPerime(Verrou(4242), Donnees, _ => "steam"));
        Assert.True(VerrouGrappe.EstPerime(Verrou(4242), Donnees, _ => "GestionMagasin"));
    }

    [Fact]
    public void Un_serveur_bien_vivant_garde_son_verrou()
    {
        Assert.False(VerrouGrappe.EstPerime(Verrou(4242), Donnees, _ => "postgres"));

        // La casse du nom de processus varie selon les systèmes.
        Assert.False(VerrouGrappe.EstPerime(Verrou(4242), Donnees, _ => "Postgres"));
    }

    [Fact]
    public void Un_processus_disparu_laisse_un_verrou_perime()
    {
        Assert.True(VerrouGrappe.EstPerime(Verrou(4242), Donnees, _ => null));
    }

    /// <summary>
    /// Un magasin peut avoir un PostgreSQL installé, avec son propre dossier
    /// de données. Effacer son verrou couperait un service qui ne nous
    /// appartient pas.
    /// </summary>
    [Fact]
    public void Le_verrou_d_une_autre_grappe_n_est_jamais_touche()
    {
        var etranger = Verrou(4242, @"C:\Program Files\PostgreSQL\16\data");

        Assert.False(VerrouGrappe.EstPerime(etranger, Donnees, _ => "postgres"));
        Assert.False(VerrouGrappe.EstPerime(etranger, Donnees, _ => null));
    }

    [Fact]
    public void Un_verrou_illisible_est_perime()
    {
        Assert.True(VerrouGrappe.EstPerime([], Donnees, _ => "postgres"));
        Assert.True(VerrouGrappe.EstPerime(["", ""], Donnees, _ => "postgres"));
        Assert.True(VerrouGrappe.EstPerime(["pas un nombre"], Donnees, _ => "postgres"));
    }

    /// <summary>
    /// Les chemins s'écrivent de plusieurs façons : barre finale, casse,
    /// segments relatifs. Une comparaison littérale prendrait notre propre
    /// verrou pour celui d'un étranger et refuserait de le retirer.
    /// </summary>
    [Fact]
    public void Le_dossier_est_reconnu_quelle_que_soit_son_ecriture()
    {
        foreach (var ecriture in new[]
                 {
                     Donnees + @"\",
                     Donnees.ToUpperInvariant(),
                     Donnees.Replace(@"\donnees", @"\autre\..\donnees")
                 })
        {
            Assert.True(
                VerrouGrappe.EstPerime(Verrou(4242, ecriture), Donnees, _ => null),
                $"« {ecriture} » n'a pas été reconnu comme notre dossier de données.");
        }
    }
}
