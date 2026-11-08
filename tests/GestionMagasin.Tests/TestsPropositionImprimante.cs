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

    [Theory]
    [InlineData("Microsoft Print to PDF")]
    [InlineData("Microsoft Print to PDF (redirection 2)")]
    [InlineData("Microsoft XPS Document Writer")]
    [InlineData("OneNote (Desktop)")]
    [InlineData("OneNote for Windows 10")]
    [InlineData("Send To OneNote 2016")]
    [InlineData("Fax")]
    [InlineData("Adobe PDF")]
    [InlineData("PDF24")]
    [InlineData("PDFCreator")]
    [InlineData("CutePDF Writer")]
    [InlineData("doPDF 11")]
    [InlineData("Foxit PDF Editor Printer")]
    public void Une_imprimante_qui_fabrique_un_fichier_est_virtuelle(string nom)
    {
        Assert.True(PropositionImprimante.EstVirtuelle(nom));
    }

    [Theory]
    [InlineData("Otala R58P")]
    [InlineData("POS-58")]
    [InlineData("XP-58")]
    [InlineData("EPSON TM-T20II Receipt")]
    [InlineData("Generic / Text Only")]
    [InlineData("HP LaserJet Pro M1080")]
    [InlineData("Caisse")]
    public void Une_vraie_imprimante_n_est_pas_virtuelle(string nom)
    {
        Assert.False(PropositionImprimante.EstVirtuelle(nom, "USB001", nom));
    }

    [Theory]
    [InlineData("PORTPROMPT:")]
    [InlineData("portprompt:")]
    [InlineData("XPSPort:")]
    [InlineData("SHRFAX:")]
    [InlineData("nul:")]
    [InlineData(@"C:\Users\Caisse\Documents\ticket.pdf")]
    public void Une_imprimante_renommee_se_trahit_par_son_port(string port)
    {
        Assert.True(PropositionImprimante.EstVirtuelle("Caisse", port));
    }

    [Theory]
    [InlineData("USB001")]
    [InlineData("COM3:")]
    [InlineData("LPT1:")]
    [InlineData("FILE:")]
    [InlineData("IP_192.168.1.50")]
    public void Un_port_d_imprimante_reelle_ou_de_fichier_brut_est_accepte(string port)
    {
        // « FILE: » avec le pilote Generic / Text Only enregistre les
        // commandes du ticket telles quelles : c'est un moyen de vérifier ce
        // qui partirait à l'imprimante, pas une imprimante PDF.
        Assert.False(PropositionImprimante.EstVirtuelle("Generic / Text Only", port, "Generic / Text Only"));
    }

    [Fact]
    public void Une_imprimante_pdf_renommee_se_trahit_par_son_pilote()
    {
        Assert.True(PropositionImprimante.EstVirtuelle("Ticket caisse", "USB001", "Microsoft Print To PDF"));
    }

    [Fact]
    public void Une_imprimante_pdf_n_est_jamais_proposee_meme_si_son_nom_evoque_la_caisse()
    {
        Assert.Null(PropositionImprimante.Proposer(["POS PDF Printer", "PDF Ticket 58mm", "Receipt to XPS"]));
    }

    [Fact]
    public void Un_nom_absent_n_est_pas_virtuel()
    {
        Assert.False(PropositionImprimante.EstVirtuelle(null));
        Assert.False(PropositionImprimante.EstVirtuelle("  ", " ", null));
    }

    [Fact]
    public void Une_liste_vide_ne_propose_rien()
    {
        Assert.Null(PropositionImprimante.Proposer([]));
    }
}
