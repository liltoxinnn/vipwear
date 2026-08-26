using GestionMagasin.ServeurEmbarque;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Choix du port d'écoute du serveur de base de données.
///
/// C'est la panne qui a immobilisé un poste : Windows réserve des plages
/// entières de ports pour Hyper-V, WSL et Docker, et les réattribue à chaque
/// démarrage de la machine. Le port fixé en dur est devenu interdit du jour
/// au lendemain, sans que rien n'ait été installé, et le logiciel ne
/// démarrait plus — « could not bind IPv4 address : Permission denied ».
/// </summary>
public class TestsSelecteurPort
{
    [Fact]
    public void Le_port_souhaite_est_retenu_lorsqu_il_est_libre()
    {
        Assert.Equal(5433, SelecteurPort.Premier(5433, _ => true));
    }

    [Fact]
    public void Un_port_interdit_par_le_systeme_fait_glisser_au_suivant()
    {
        // Windows peut refuser une plage entière : ici 5433 à 5442.
        var interdits = Enumerable.Range(5433, 10).ToHashSet();

        Assert.Equal(5443, SelecteurPort.Premier(5433, p => !interdits.Contains(p)));
    }

    /// <summary>
    /// Aucun port disponible doit se solder par un refus explicite, et non
    /// par un port hasardeux sur lequel personne n'écoute.
    /// </summary>
    [Fact]
    public void Aucun_port_disponible_ne_rend_rien()
    {
        Assert.Null(SelecteurPort.Premier(5433, _ => false));
    }

    [Fact]
    public void La_recherche_ne_depasse_pas_le_dernier_port_valide()
    {
        // Depuis 65530, il ne reste que six numéros valides.
        Assert.Null(SelecteurPort.Premier(65530, _ => false, etendue: 100));
    }

    /// <summary>
    /// L'épreuve consiste à ouvrir le port pour de bon : demander au système
    /// la liste des ports occupés ne dirait rien des plages qu'il s'est
    /// réservées, et ce sont elles qui font échouer le serveur.
    /// </summary>
    [Fact]
    public void Un_port_deja_ouvert_est_ecarte()
    {
        var occupe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        occupe.Start();

        try
        {
            var port = ((System.Net.IPEndPoint)occupe.LocalEndpoint).Port;

            Assert.False(SelecteurPort.Utilisable(port));

            var retenu = SelecteurPort.Premier(port, SelecteurPort.Utilisable);

            Assert.NotNull(retenu);
            Assert.NotEqual(port, retenu);
        }
        finally
        {
            occupe.Stop();
        }
    }

    [Fact]
    public void Un_port_libre_est_reconnu_utilisable_et_rendu_apres_l_epreuve()
    {
        var port = SelecteurPort.Premier(5433, SelecteurPort.Utilisable);

        Assert.NotNull(port);

        // L'épreuve doit refermer ce qu'elle a ouvert : sinon le serveur
        // trouverait occupé le port qu'on vient de lui désigner.
        Assert.True(SelecteurPort.Utilisable(port.Value));
    }
}
