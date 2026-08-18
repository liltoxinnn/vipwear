using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Securite;
using GestionMagasin.Tests.Socle;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Enchaînement des sessions sur un même poste. Un magasin ouvre et ferme des
/// sessions toute la journée : le responsable prend la caisse le midi, la
/// caissière la reprend ensuite. Se reconnecter doit fonctionner autant de
/// fois que nécessaire, et une session fermée ne doit conserver aucun droit.
/// </summary>
public class TestsSessions : BaseDeTest
{
    private IServiceAuthentification Authentification => Service<IServiceAuthentification>();

    [Fact]
    public async Task Il_est_possible_de_se_reconnecter_apres_une_deconnexion()
    {
        // Trois cycles complets : le poste doit se comporter à l'identique au
        // premier comme au troisième.
        for (var cycle = 1; cycle <= 3; cycle++)
        {
            var connexion = await Authentification.ConnecterAsync(
                ComptesParDefaut.NomUtilisateurAdministrateur,
                ComptesParDefaut.MotDePasseAdministrateur);

            Assert.True(connexion.Reussie, $"La connexion a échoué au cycle {cycle}.");
            Assert.True(Session.EstAuthentifie);

            await Authentification.DeconnecterAsync();

            Assert.False(Session.EstAuthentifie);
        }
    }

    [Fact]
    public async Task Une_session_fermee_ne_conserve_aucune_autorisation()
    {
        await Authentification.ConnecterAsync(
            ComptesParDefaut.NomUtilisateurAdministrateur,
            ComptesParDefaut.MotDePasseAdministrateur);

        Assert.NotEmpty(Session.PermissionsActives());

        await Authentification.DeconnecterAsync();

        Assert.Empty(Session.PermissionsActives());
        Assert.False(Session.Possede(CodesPermissions.GererParametres));
        Assert.Equal(string.Empty, Session.NomUtilisateur);
    }

    [Fact]
    public async Task Une_seconde_session_remplace_entierement_la_precedente()
    {
        var caissier = await ConnecterAsync(NomsRoles.Caissier);

        Assert.Equal(caissier.NomUtilisateur, Session.NomUtilisateur);
        Assert.False(Session.Possede(CodesPermissions.GererParametres));

        await Authentification.DeconnecterAsync();

        // Le compte suivant ne doit hériter d'aucun droit du précédent, ni
        // l'inverse : chaque session repart des permissions de son rôle.
        await ConnecterAsync(NomsRoles.Administrateur);

        Assert.True(Session.Possede(CodesPermissions.GererParametres));
        Assert.NotEqual(caissier.NomUtilisateur, Session.NomUtilisateur);
    }
}
