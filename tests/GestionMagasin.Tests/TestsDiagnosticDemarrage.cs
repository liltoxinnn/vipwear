using GestionMagasin.ServeurEmbarque;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Raison affichée quand la base de données ne démarre pas.
///
/// Le journal du serveur garde les lignes de toutes les sessions. On y
/// prenait la dernière erreur venue : le magasin voyait une ligne vieille de
/// six jours — la trace d'un arrêt normal — au lieu de la vraie raison.
/// </summary>
public class TestsDiagnosticDemarrage
{
    private const string AttenteDepassee =
        "waiting for server to start........................ stopped waiting\n" +
        "pg_ctl: server did not start in time";

    private const string EchecSansDetail =
        "waiting for server to start.... stopped waiting\n" +
        "pg_ctl: could not start server\n" +
        "Examine the log output.";

    [Fact]
    public void Une_attente_depassee_est_expliquee_meme_si_le_serveur_n_a_rien_ecrit()
    {
        var cause = DiagnosticDemarrage.Expliquer(journalDeLaTentative: "", AttenteDepassee);

        Assert.NotNull(cause);
        Assert.Contains("trop de temps", cause);
    }

    [Fact]
    public void Sans_rien_de_neuf_au_journal_le_message_de_pg_ctl_est_rapporte()
    {
        var cause = DiagnosticDemarrage.Expliquer(null, EchecSansDetail);

        Assert.Equal("pg_ctl: could not start server", cause);
    }

    [Fact]
    public void Le_journal_de_la_tentative_l_emporte_sur_pg_ctl()
    {
        var journal =
            "2026-11-14 10:02:11.120 WAT [4120] LOG:  starting PostgreSQL 17.2\n" +
            "2026-11-14 10:02:11.200 WAT [4120] FATAL:  pre-existing shared memory block is still in use\n" +
            "2026-11-14 10:02:11.200 WAT [4120] HINT:  Terminate any old server processes.";

        var cause = DiagnosticDemarrage.Expliquer(journal, EchecSansDetail);

        Assert.NotNull(cause);
        Assert.Contains("session précédente tourne encore", cause);
    }

    [Theory]
    [InlineData("FATAL:  lock file \"postmaster.pid\" already exists", "ne s'est pas refermée")]
    [InlineData("LOG:  could not bind IPv4 address \"127.0.0.1\": Permission denied", "port")]
    [InlineData("FATAL:  database files are incompatible with server", "autre version")]
    [InlineData("FATAL:  could not write lock file: No space left on device", "disque est plein")]
    public void Les_causes_connues_sont_traduites(string ligne, string attendu)
    {
        var cause = DiagnosticDemarrage.Expliquer($"2026-11-14 10:00:00 WAT [1] {ligne}", EchecSansDetail);

        Assert.NotNull(cause);
        Assert.Contains(attendu, cause);
    }

    [Fact]
    public void Une_erreur_inconnue_de_la_tentative_est_rapportee_telle_quelle()
    {
        var journal = "2026-11-14 10:00:00 WAT [1] FATAL:  something unexpected happened";

        Assert.Equal(journal, DiagnosticDemarrage.Expliquer(journal, EchecSansDetail));
    }

    [Fact]
    public void Rien_a_dire_ne_produit_aucune_cause_inventee()
    {
        Assert.Null(DiagnosticDemarrage.Expliquer(null, null));
        Assert.Null(DiagnosticDemarrage.Expliquer("", "waiting for server to start...."));
    }
}
