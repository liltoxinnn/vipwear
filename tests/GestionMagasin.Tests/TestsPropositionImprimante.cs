using GestionMagasin.Application.Impression;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Repérage de l'imprimante de tickets parmi celles de Windows.
///
/// La proposition ne fait que présélectionner une ligne : se tromper ne
/// coûte qu'un clic. Mais elle ne doit jamais désigner une imprimante
/// virtuelle — un ticket « imprimé » dans un PDF que personne n'ouvre est
/// un ticket perdu.
/// </summary>
public class TestsPropositionImprimante
{
    [Theory]
    [InlineData("POS-58")]
    [InlineData("POS58 Printer")]
    [InlineData("XP-58")]
    [InlineData("Otala R58P")]
    [InlineData("Thermal Receipt Printer")]
    [InlineData("Imprimante thermique 58mm")]
    [InlineData("Generic / Text Only")]
    [InlineData("EPSON TM-T20II Receipt")]
    public void Une_imprimante_de_caisse_est_reconnue_a_son_nom(string nom)
    {
        var proposee = PropositionImprimante.Proposer(["Microsoft Print to PDF", nom, "HP LaserJet 1020"]);

        Assert.NotNull(proposee);
        Assert.Equal(nom, proposee.Nom);
    }

    [Fact]
    public void Aucune_imprimante_virtuelle_ou_de_bureau_n_est_proposee()
    {
        var proposee = PropositionImprimante.Proposer(
        [
            "Microsoft Print to PDF",
            "Microsoft XPS Document Writer",
            "OneNote (Desktop)",
            "Fax",
            "HP LaserJet Pro M1080",
            "Canon iR-ADV C5840"
        ]);

        Assert.Null(proposee);
    }

    [Theory]
    [InlineData("POS-58", LargeurRouleau.Rouleau58)]
    [InlineData("XP-80C", LargeurRouleau.Rouleau80)]
    [InlineData("Imprimante 80 mm", LargeurRouleau.Rouleau80)]
    [InlineData("Generic / Text Only", LargeurRouleau.Rouleau58)]
    [InlineData("Otala R58P", LargeurRouleau.Rouleau58)]
    public void La_largeur_du_rouleau_se_lit_dans_le_nom(string nom, LargeurRouleau attendue)
    {
        Assert.Equal(attendue, PropositionImprimante.LargeurSupposee(nom));
    }

    [Fact]
    public void Un_numero_de_modele_qui_contient_80_n_est_pas_un_rouleau_de_80()
    {
        Assert.Equal(LargeurRouleau.Rouleau58, PropositionImprimante.LargeurSupposee("HP LaserJet 1080"));
    }

    [Fact]
    public void Une_liste_vide_ne_propose_rien()
    {
        Assert.Null(PropositionImprimante.Proposer([]));
    }
}
