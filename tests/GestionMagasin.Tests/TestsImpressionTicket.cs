using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Impression;
using GestionMagasin.Domain.Entities;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Ticket de caisse sur imprimante thermique.
///
/// L'imprimante ne peut pas être branchée ici : tout ce qui se vérifie sans
/// elle l'est. Le ticket est composé en colonnes, converti en points puis en
/// commandes ESC/POS ; chacune de ces étapes produit du texte ou des octets
/// qu'on peut lire. Seuls restent hors de portée le dessin des lettres par
/// Windows et l'envoi au spouleur.
/// </summary>
public class TestsImpressionTicket
{
    private static readonly ReglagesTicket Rouleau58 = new() { NomImprimante = "POS-58" };

    private static readonly ReglagesTicket Rouleau80 =
        new() { NomImprimante = "POS-80", Largeur = LargeurRouleau.Rouleau80 };

    private static ParametresMagasin Magasin() => new()
    {
        NomMagasin = "VIP MEN'S STORE",
        Adresse = "Rue de la Liberté, Béjaïa",
        Telephone = "0550 00 00 00",
        RegistreCommerce = "06/00-1234567B21",
        SymboleDevise = "DA",
        DelaiRetourJours = 30,
        InformationsTicket = "Merci de votre visite !"
    };

    private static VenteDto Vente(params LigneVenteDto[] lignes)
    {
        var sousTotal = lignes.Sum(l => l.Total);

        return new VenteDto
        {
            NumeroVente = "VTE-2026-000042",
            DateVente = new DateTime(2026, 11, 5, 14, 32, 0, DateTimeKind.Local),
            UtilisateurNom = "Compte Administrateur",
            SousTotal = sousTotal,
            Total = sousTotal,
            MontantPaye = sousTotal,
            Lignes = lignes,
            Paiements = [new PaiementDto { LibelleModePaiement = "Espèces", Montant = sousTotal }]
        };
    }

    private static LigneVenteDto Article(string designation, int quantite, decimal prix, decimal remise = 0m) => new()
    {
        Designation = designation,
        Quantite = quantite,
        PrixUnitaire = prix,
        Remise = remise,
        Total = quantite * prix - remise
    };

    private static int Largeur(LigneTicket ligne, ReglagesTicket reglages) =>
        ligne.Style == StyleLigne.Grand
            ? CompositionTicket.ColonnesGrandes(reglages.Colonnes)
            : reglages.Colonnes;

    // ==================================================================
    // Réglages
    // ==================================================================

    [Fact]
    public void Un_rouleau_de_58_mm_imprime_32_caracteres_sur_384_points()
    {
        Assert.Equal(384, Rouleau58.PointsParLigne);
        Assert.Equal(32, Rouleau58.Colonnes);
        Assert.Equal(576, Rouleau80.PointsParLigne);
        Assert.Equal(48, Rouleau80.Colonnes);
    }

    [Fact]
    public void Les_reglages_d_origine_ne_designent_aucune_imprimante()
    {
        var origine = new ReglagesTicket();

        // Tant que rien n'est choisi, le logiciel garde son ancien
        // comportement : il produit un PDF.
        Assert.False(origine.EstConfiguree);
        Assert.Equal(LargeurRouleau.Rouleau58, origine.Largeur);
        Assert.False(origine.ImprimerSansDemander);
        Assert.False(origine.OuvrirTiroir);
        Assert.Equal(1, origine.Exemplaires);
    }

    [Fact]
    public void Les_reglages_se_relisent_tels_qu_ils_ont_ete_ecrits()
    {
        var ecrits = new ReglagesTicket
        {
            NomImprimante = "POS-58 (Copie 1)",
            Largeur = LargeurRouleau.Rouleau80,
            ImprimerSansDemander = true,
            CouperPapier = false,
            OuvrirTiroir = true,
            ImprimerLogo = false,
            Exemplaires = 2
        };

        Assert.Equal(ecrits, ReglagesTicket.DepuisJson(ecrits.VersJson()));
    }

    /// <summary>
    /// Une coupure de courant pendant l'écriture laisse un fichier tronqué.
    /// La caisse doit pouvoir vendre malgré tout.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ \"NomImprimante\": \"POS")]
    [InlineData("n'importe quoi")]
    public void Un_fichier_de_reglages_abime_rend_les_reglages_d_origine(string? contenu)
    {
        Assert.Equal(new ReglagesTicket(), ReglagesTicket.DepuisJson(contenu));
    }

    [Fact]
    public void Les_valeurs_hors_limites_sont_ramenees_a_ce_qui_s_imprime()
    {
        var relus = ReglagesTicket.DepuisJson(
            "{ \"NomImprimante\": \"  POS-58  \", \"Largeur\": 99, \"Exemplaires\": 40 }");

        Assert.Equal("POS-58", relus.NomImprimante);
        Assert.Equal(LargeurRouleau.Rouleau58, relus.Largeur);
        Assert.Equal(3, relus.Exemplaires);
    }

    // ==================================================================
    // Composition
    // ==================================================================

    /// <summary>
    /// Une ligne qui dépasse la largeur du papier est perdue : la tête n'a
    /// pas d'endroit où l'imprimer. C'est la règle à ne jamais enfreindre.
    /// </summary>
    [Theory]
    [InlineData(LargeurRouleau.Rouleau58)]
    [InlineData(LargeurRouleau.Rouleau80)]
    public void Aucune_ligne_ne_deborde_du_papier(LargeurRouleau largeur)
    {
        var reglages = new ReglagesTicket { NomImprimante = "POS", Largeur = largeur };

        var magasin = Magasin();
        magasin.NomMagasin = "VIP MEN'S STORE — PRÊT-À-PORTER MASCULIN DE QUALITÉ";
        magasin.Adresse = "Cité des 1000 logements, bâtiment 12, entrée B, Béjaïa 06000, Algérie";
        magasin.InformationsTicket = "Un message volontairement très long pour vérifier qu'il se replie proprement";

        var vente = Vente(
            Article("PANTALON CHINO SLIM STRETCH COTON BIOLOGIQUE — Bleu marine / 42", 1, 125_000m),
            Article("Ceinture", 12, 1_200m, remise: 400m),
            Article("ChemiseAvecUnNomSansAucuneEspaceQuiNeTientPasSurUneLigne — Blanc / XL", 3, 2_300m));

        var lignes = CompositionTicket.Composer(vente, magasin, reglages, montantRecu: 200_000m);

        Assert.NotEmpty(lignes);

        foreach (var ligne in lignes)
        {
            Assert.True(ligne.Texte.Length <= Largeur(ligne, reglages),
                $"« {ligne.Texte} » ({ligne.Texte.Length} car.) dépasse {Largeur(ligne, reglages)} en {ligne.Style}");
        }
    }

    [Fact]
    public void Les_montants_tombent_tous_sur_le_bord_droit()
    {
        var lignes = CompositionTicket.Composer(
            Vente(Article("HOODIE — Rouge / S", 2, 4_000m)), Magasin(), Rouleau58);

        var montants = lignes
            .Where(l => l.Style != StyleLigne.Grand && l.Texte.EndsWith(" DA", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(montants);
        Assert.All(montants, l => Assert.Equal(Rouleau58.Colonnes, l.Texte.Length));
    }

    [Fact]
    public void Le_ticket_porte_l_article_sa_quantite_et_le_total()
    {
        var lignes = CompositionTicket.Composer(
            Vente(Article("HOODIE — Rouge / S", 2, 4_000m)), Magasin(), Rouleau58);

        var texte = string.Join('\n', lignes.Select(l => l.Texte));

        Assert.Contains("HOODIE — Rouge / S", texte);
        Assert.Contains("2 × 4 000,00", texte);
        Assert.Contains("8 000,00 DA", texte);
        Assert.Contains("Ticket n° VTE-2026-000042", texte);

        var total = Assert.Single(lignes, l => l.Texte.StartsWith("TOTAL", StringComparison.Ordinal));

        Assert.Equal(StyleLigne.Grand, total.Style);
        Assert.EndsWith("8 000,00 DA", total.Texte);
    }

    /// <summary>
    /// Le texte part tel quel vers l'image : les accents ne passent par
    /// aucune table de caractères de l'imprimante, et ne peuvent donc pas
    /// s'y perdre.
    /// </summary>
    [Fact]
    public void Les_accents_sont_conserves()
    {
        var lignes = CompositionTicket.Composer(
            Vente(Article("Veste été", 1, 9_000m)), Magasin(), Rouleau58);

        var texte = string.Join('\n', lignes.Select(l => l.Texte));

        Assert.Contains("Veste été", texte);
        Assert.Contains("Béjaïa", texte);
        Assert.Contains("Échange ou retour", texte);
    }

    [Fact]
    public void La_monnaie_rendue_est_imprimee_quand_le_client_a_donne_plus()
    {
        var lignes = CompositionTicket.Composer(
            Vente(Article("HOODIE — Rouge / S", 1, 2_300m)), Magasin(), Rouleau58, montantRecu: 5_000m);

        var rendu = Assert.Single(lignes, l => l.Texte.StartsWith("Rendu", StringComparison.Ordinal));

        Assert.EndsWith("2 700,00 DA", rendu.Texte);
        Assert.Contains(lignes, l => l.Texte.StartsWith("Reçu", StringComparison.Ordinal)
                                     && l.Texte.EndsWith("5 000,00 DA", StringComparison.Ordinal));
    }

    /// <summary>
    /// Une réimpression ne connaît plus la somme remise : le ticket n'en
    /// invente pas. L'appoint exact n'a rien à vérifier non plus.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(2_300)]
    public void Sans_monnaie_a_rendre_le_ticket_n_en_parle_pas(int? recu)
    {
        var lignes = CompositionTicket.Composer(
            Vente(Article("HOODIE — Rouge / S", 1, 2_300m)), Magasin(), Rouleau58, montantRecu: recu);

        Assert.DoesNotContain(lignes, l => l.Texte.StartsWith("Rendu", StringComparison.Ordinal));
        Assert.DoesNotContain(lignes, l => l.Texte.StartsWith("Reçu", StringComparison.Ordinal));
    }

    [Fact]
    public void Une_designation_trop_longue_tient_sur_deux_lignes_et_le_signale()
    {
        var designation = "PANTALON CHINO SLIM STRETCH COTON BIOLOGIQUE LAVÉ — Bleu marine / 42";

        var lignes = CompositionTicket.Composer(
            Vente(Article(designation, 1, 1_000m)), Magasin(), Rouleau58);

        var gras = lignes.Where(l => l.Style == StyleLigne.Gras && !l.Texte.StartsWith("Ticket", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, gras.Count);
        Assert.EndsWith("…", gras[1].Texte);
    }

    [Fact]
    public void Le_bandeau_d_essai_s_imprime_en_grand_sous_l_en_tete()
    {
        var lignes = CompositionTicket.Composer(
            Vente(Article("HOODIE", 1, 1_000m)), Magasin(), Rouleau58, mention: "TICKET D'ESSAI");

        Assert.Contains(lignes, l => l.Style == StyleLigne.Grand && l.Texte.Trim() == "TICKET D'ESSAI");
    }

    [Fact]
    public void Un_libelle_trop_long_laisse_le_montant_entier_a_la_ligne_suivante()
    {
        var morceaux = CompositionTicket.DeuxColonnes("Un libellé beaucoup trop long pour la ligne", "125 000,00 DA", 32);

        Assert.Equal(2, morceaux.Count);
        Assert.Equal(32, morceaux[0].Length);
        Assert.Equal("                   125 000,00 DA", morceaux[1]);
    }

    [Fact]
    public void Un_tiret_ne_reste_jamais_seul_en_fin_de_ligne()
    {
        var lignes = CompositionTicket.Envelopper("PANTALON CHINO SLIM STRETCH — Beige / 42", 32);

        Assert.Equal(["PANTALON CHINO SLIM STRETCH", "— Beige / 42"], lignes);

        // Le texte rendu ne garde aucune espace insécable.
        Assert.DoesNotContain(lignes, l => l.Contains('\u00A0'));
    }

    [Fact]
    public void Un_mot_plus_long_que_la_ligne_est_coupe_net()
    {
        var lignes = CompositionTicket.Envelopper("abcdefghij klm", 4);

        Assert.Equal(["abcd", "efgh", "ij", "klm"], lignes);
    }

    // ==================================================================
    // Points
    // ==================================================================

    [Fact]
    public void Le_noir_brule_un_point_et_le_blanc_le_laisse()
    {
        // Huit pixels : noir, blanc, noir, blanc...
        byte[] gris = [0, 255, 0, 255, 0, 255, 0, 255];

        var points = Monochrome.VersPoints(gris, 8, 1);

        // Le premier pixel occupe le bit de poids fort : 1010 1010.
        Assert.Equal([0b1010_1010], points);
    }

    [Fact]
    public void Une_rangee_incomplete_est_completee_jusqu_a_l_octet()
    {
        // Dix pixels noirs : deux octets, dont six bits inutilisés à blanc.
        var points = Monochrome.VersPoints(Enumerable.Repeat((byte)0, 10).ToArray(), 10, 1);

        Assert.Equal([0xFF, 0b1100_0000], points);
    }

    /// <summary>
    /// Un gris moyen seuillé devient tout blanc ; tramé, il devient une
    /// moitié de points. C'est ce qui garde le logo visible.
    /// </summary>
    [Fact]
    public void Un_gris_tramé_garde_sa_densite_et_un_gris_seuille_la_perd()
    {
        const int Cote = 64;
        var gris = Enumerable.Repeat((byte)140, Cote * Cote).ToArray();

        static int Noirs(byte[] points) => points.Sum(o => System.Numerics.BitOperations.PopCount(o));

        var seuille = Monochrome.VersPoints(gris, Cote, Cote, rangeesTramees: 0);
        var trame = Monochrome.VersPoints(gris, Cote, Cote, rangeesTramees: Cote);

        Assert.Equal(0, Noirs(seuille));

        // 140 sur 255 : environ 45 % de noir attendu.
        var proportion = Noirs(trame) / (double)(Cote * Cote);

        Assert.InRange(proportion, 0.40, 0.50);
    }

    /// <summary>
    /// Le texte passe dans la trame quand il croise le logo : un pixel déjà
    /// tout noir ou tout blanc ne doit rien répartir autour de lui.
    /// </summary>
    [Fact]
    public void La_trame_ne_brouille_pas_le_noir_et_blanc_pur()
    {
        byte[] gris = [0, 255, 255, 0, 0, 255, 255, 0];

        Assert.Equal(
            Monochrome.VersPoints(gris, 8, 1, rangeesTramees: 0),
            Monochrome.VersPoints(gris, 8, 1, rangeesTramees: 1));
    }

    // ==================================================================
    // Commandes ESC/POS
    // ==================================================================

    private static byte[] Ticket(int hauteur, ReglagesTicket reglages)
    {
        var points = new byte[Monochrome.OctetsParRangee(384) * hauteur];

        return EscPos.Ticket(points, 384, hauteur, reglages);
    }

    private static int Occurrences(byte[] contenu, byte[] motif)
    {
        var compte = 0;

        for (var i = 0; i <= contenu.Length - motif.Length; i++)
        {
            if (contenu.AsSpan(i, motif.Length).SequenceEqual(motif))
            {
                compte++;
            }
        }

        return compte;
    }

    [Fact]
    public void Le_travail_commence_par_remettre_l_imprimante_a_zero()
    {
        var travail = Ticket(10, Rouleau58);

        Assert.Equal(EscPos.Initialiser, travail[..2]);
    }

    [Fact]
    public void L_entete_d_image_porte_la_largeur_en_octets_et_la_hauteur()
    {
        // 384 points = 48 octets ; 300 rangées = 0x012C.
        Assert.Equal(
            [0x1D, 0x76, 0x30, 0x00, 48, 0, 0x2C, 0x01],
            EscPos.EnteteImage(48, 300));
    }

    /// <summary>
    /// La mémoire de réception d'une petite imprimante est réduite : un
    /// ticket d'un seul bloc peut la dépasser et la fin se perd.
    /// </summary>
    [Fact]
    public void Un_ticket_long_part_par_bandes_qui_couvrent_toute_l_image()
    {
        const int Hauteur = 300;

        var travail = Ticket(Hauteur, Rouleau58 with { CouperPapier = false });

        // 300 rangées en bandes de 128 : 128 + 128 + 44.
        Assert.Equal(2, Occurrences(travail, EscPos.EnteteImage(48, 128)));
        Assert.Equal(1, Occurrences(travail, EscPos.EnteteImage(48, 44)));

        // Octets d'image + en-têtes + initialisation + avance.
        Assert.Equal(2 + 3 * 8 + 48 * Hauteur + 3, travail.Length);
    }

    [Fact]
    public void La_coupe_et_le_tiroir_n_ont_lieu_que_s_ils_sont_demandes()
    {
        var sobre = Ticket(10, Rouleau58 with { CouperPapier = false, OuvrirTiroir = false });
        var complet = Ticket(10, Rouleau58 with { CouperPapier = true, OuvrirTiroir = true });

        Assert.Equal(0, Occurrences(sobre, EscPos.Couper));
        Assert.Equal(0, Occurrences(sobre, EscPos.OuvrirTiroir));

        Assert.Equal(1, Occurrences(complet, EscPos.Couper));
        Assert.Equal(1, Occurrences(complet, EscPos.OuvrirTiroir));

        // Le tiroir s'ouvre avant l'impression, juste après la remise à zéro.
        Assert.Equal(EscPos.OuvrirTiroir, complet[2..7]);
    }

    /// <summary>
    /// Sur une imprimante sans massicot la coupe est ignorée : sans avance,
    /// la fin du ticket resterait sous la barre de découpe.
    /// </summary>
    [Fact]
    public void Le_papier_avance_toujours_avant_la_coupe()
    {
        var travail = Ticket(10, Rouleau58 with { CouperPapier = true });

        var avance = EscPos.Avancer(EscPos.AvanceFinale);
        var finAttendue = avance.Concat(EscPos.Couper).ToArray();

        Assert.Equal(finAttendue, travail[^finAttendue.Length..]);
    }

    [Fact]
    public void Chaque_exemplaire_est_imprime_et_coupe_mais_le_tiroir_ne_s_ouvre_qu_une_fois()
    {
        var travail = Ticket(10, Rouleau58 with { Exemplaires = 2, CouperPapier = true, OuvrirTiroir = true });

        Assert.Equal(2, Occurrences(travail, EscPos.EnteteImage(48, 10)));
        Assert.Equal(2, Occurrences(travail, EscPos.Couper));
        Assert.Equal(1, Occurrences(travail, EscPos.OuvrirTiroir));
    }
}
