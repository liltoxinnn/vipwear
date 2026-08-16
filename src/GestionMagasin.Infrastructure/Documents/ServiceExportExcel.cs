using ClosedXML.Excel;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;

namespace GestionMagasin.Infrastructure.Documents;

/// <summary>
/// Exports Excel réalisés avec ClosedXML. Les classeurs produits sont
/// directement exploitables : en-têtes figés, filtres actifs, colonnes
/// ajustées, montants au format monétaire et libellés en français.
/// </summary>
public class ServiceExportExcel : IServiceExportExcel
{
    private const string FormatMontant = "# ##0.00";
    private const string FormatDate = "dd/MM/yyyy";
    private const string FormatDateHeure = "dd/MM/yyyy HH:mm";

    private static readonly XLColor CouleurEntete = XLColor.FromHtml("#1F3864");
    private static readonly XLColor CouleurTotal = XLColor.FromHtml("#EEF2F9");

    public Task<byte[]> ExporterStockAsync(
        IReadOnlyList<LigneStockDto> lignes,
        CancellationToken jeton = default)
    {
        using var classeur = new XLWorkbook();
        var feuille = classeur.AddWorksheet("Stock");

        string[] entetes =
        [
            "Produit", "Référence", "Code article", "Code-barres", "Marque",
            "Couleur", "Taille", "Quantité", "Seuil d'alerte", "Situation",
            "Prix d'achat", "Prix de vente", "Valeur du stock"
        ];

        EcrireEntetes(feuille, entetes);

        var ligneCourante = 2;

        foreach (var ligne in lignes)
        {
            feuille.Cell(ligneCourante, 1).Value = ligne.ProduitNom;
            feuille.Cell(ligneCourante, 2).Value = ligne.Reference;
            feuille.Cell(ligneCourante, 3).Value = ligne.Sku;
            feuille.Cell(ligneCourante, 4).Value = ligne.CodeBarres ?? string.Empty;
            feuille.Cell(ligneCourante, 5).Value = ligne.Marque ?? string.Empty;
            feuille.Cell(ligneCourante, 6).Value = ligne.Couleur;
            feuille.Cell(ligneCourante, 7).Value = ligne.Taille;
            feuille.Cell(ligneCourante, 8).Value = ligne.QuantiteDisponible;
            feuille.Cell(ligneCourante, 9).Value = ligne.SeuilMinimum;
            feuille.Cell(ligneCourante, 10).Value = ligne.LibelleEtat;
            feuille.Cell(ligneCourante, 11).Value = ligne.PrixAchat;
            feuille.Cell(ligneCourante, 12).Value = ligne.PrixVente;
            feuille.Cell(ligneCourante, 13).Value = ligne.ValeurStock;

            // Les situations critiques sautent aux yeux dans le tableur.
            if (ligne.EnRupture)
            {
                feuille.Cell(ligneCourante, 10).Style.Font.FontColor = XLColor.FromHtml("#C0392B");
                feuille.Cell(ligneCourante, 10).Style.Font.Bold = true;
            }
            else if (ligne.StockFaible)
            {
                feuille.Cell(ligneCourante, 10).Style.Font.FontColor = XLColor.FromHtml("#B7791F");
                feuille.Cell(ligneCourante, 10).Style.Font.Bold = true;
            }

            ligneCourante++;
        }

        AppliquerFormatMontant(feuille, 11, 13, 2, ligneCourante - 1);

        if (lignes.Count > 0)
        {
            EcrireLigneTotal(feuille, ligneCourante, "TOTAL", new Dictionary<int, object>
            {
                [8] = lignes.Sum(l => l.QuantiteDisponible),
                [13] = lignes.Sum(l => l.ValeurStock)
            });

            feuille.Cell(ligneCourante, 13).Style.NumberFormat.Format = FormatMontant;
        }

        Finaliser(feuille, entetes.Length, ligneCourante - 1);

        return Task.FromResult(Serialiser(classeur));
    }

    public Task<byte[]> ExporterProduitsAsync(
        IReadOnlyList<ProduitDto> produits,
        CancellationToken jeton = default)
    {
        using var classeur = new XLWorkbook();

        // Première feuille : la fiche produit.
        var feuilleProduits = classeur.AddWorksheet("Produits");

        string[] entetesProduits =
        [
            "Référence", "Code article", "Nom", "Marque", "Collection", "Saison",
            "Prix d'achat", "Prix de vente", "Marge", "Taux de marge (%)",
            "Déclinaisons", "Stock total", "État", "Créé le"
        ];

        EcrireEntetes(feuilleProduits, entetesProduits);

        var ligne = 2;

        foreach (var produit in produits)
        {
            feuilleProduits.Cell(ligne, 1).Value = produit.Reference;
            feuilleProduits.Cell(ligne, 2).Value = produit.Sku;
            feuilleProduits.Cell(ligne, 3).Value = produit.Nom;
            feuilleProduits.Cell(ligne, 4).Value = produit.Marque ?? string.Empty;
            feuilleProduits.Cell(ligne, 5).Value = produit.Collection ?? string.Empty;
            feuilleProduits.Cell(ligne, 6).Value = produit.Saison ?? string.Empty;
            feuilleProduits.Cell(ligne, 7).Value = produit.PrixAchat;
            feuilleProduits.Cell(ligne, 8).Value = produit.PrixVente;
            feuilleProduits.Cell(ligne, 9).Value = produit.Marge;
            feuilleProduits.Cell(ligne, 10).Value = produit.TauxMarge;
            feuilleProduits.Cell(ligne, 11).Value = produit.NombreVariantes;
            feuilleProduits.Cell(ligne, 12).Value = produit.StockTotal;
            feuilleProduits.Cell(ligne, 13).Value = produit.Actif ? "Actif" : "Inactif";
            feuilleProduits.Cell(ligne, 14).Value = produit.DateCreation.ToLocalTime();
            feuilleProduits.Cell(ligne, 14).Style.DateFormat.Format = FormatDate;

            ligne++;
        }

        AppliquerFormatMontant(feuilleProduits, 7, 9, 2, ligne - 1);
        Finaliser(feuilleProduits, entetesProduits.Length, ligne - 1);

        // Seconde feuille : le détail des déclinaisons, une ligne par article
        // réellement scannable en caisse.
        var feuilleVariantes = classeur.AddWorksheet("Déclinaisons");

        string[] entetesVariantes =
        [
            "Produit", "Référence produit", "Code article", "Code-barres",
            "Couleur", "Taille", "Prix d'achat", "Prix de vente",
            "Seuil d'alerte", "Stock", "État"
        ];

        EcrireEntetes(feuilleVariantes, entetesVariantes);

        ligne = 2;

        foreach (var produit in produits)
        {
            foreach (var variante in produit.Variantes)
            {
                feuilleVariantes.Cell(ligne, 1).Value = produit.Nom;
                feuilleVariantes.Cell(ligne, 2).Value = produit.Reference;
                feuilleVariantes.Cell(ligne, 3).Value = variante.Sku;
                feuilleVariantes.Cell(ligne, 4).Value = variante.CodeBarres ?? string.Empty;
                feuilleVariantes.Cell(ligne, 5).Value = variante.Couleur;
                feuilleVariantes.Cell(ligne, 6).Value = variante.Taille;
                feuilleVariantes.Cell(ligne, 7).Value = variante.PrixAchat;
                feuilleVariantes.Cell(ligne, 8).Value = variante.PrixVente;
                feuilleVariantes.Cell(ligne, 9).Value = variante.SeuilMinimum;
                feuilleVariantes.Cell(ligne, 10).Value = variante.QuantiteDisponible;
                feuilleVariantes.Cell(ligne, 11).Value = variante.LibelleEtat;

                ligne++;
            }
        }

        AppliquerFormatMontant(feuilleVariantes, 7, 8, 2, ligne - 1);
        Finaliser(feuilleVariantes, entetesVariantes.Length, ligne - 1);

        return Task.FromResult(Serialiser(classeur));
    }

    public Task<byte[]> ExporterVentesAsync(
        IReadOnlyList<ResumeVenteDto> ventes,
        Periode periode,
        CancellationToken jeton = default)
    {
        using var classeur = new XLWorkbook();
        var feuille = classeur.AddWorksheet("Ventes");

        EcrireTitrePeriode(feuille, "Ventes", periode, 9);

        string[] entetes =
        [
            "N° de vente", "Date", "Client", "Vendeur", "Articles",
            "Total", "Montant payé", "Reste à payer", "Statut"
        ];

        EcrireEntetes(feuille, entetes, 3);

        var ligne = 4;

        foreach (var vente in ventes)
        {
            feuille.Cell(ligne, 1).Value = vente.NumeroVente;
            feuille.Cell(ligne, 2).Value = vente.DateVente.ToLocalTime();
            feuille.Cell(ligne, 2).Style.DateFormat.Format = FormatDateHeure;
            feuille.Cell(ligne, 3).Value = vente.ClientNom ?? "Client de passage";
            feuille.Cell(ligne, 4).Value = vente.UtilisateurNom;
            feuille.Cell(ligne, 5).Value = vente.NombreArticles;
            feuille.Cell(ligne, 6).Value = vente.Total;
            feuille.Cell(ligne, 7).Value = vente.MontantPaye;
            feuille.Cell(ligne, 8).Value = vente.Total - vente.MontantPaye;
            feuille.Cell(ligne, 9).Value = vente.LibelleStatut;

            ligne++;
        }

        AppliquerFormatMontant(feuille, 6, 8, 4, ligne - 1);

        if (ventes.Count > 0)
        {
            EcrireLigneTotal(feuille, ligne, "TOTAL", new Dictionary<int, object>
            {
                [5] = ventes.Sum(v => v.NombreArticles),
                [6] = ventes.Sum(v => v.Total),
                [7] = ventes.Sum(v => v.MontantPaye),
                [8] = ventes.Sum(v => v.Total - v.MontantPaye)
            });

            AppliquerFormatMontant(feuille, 6, 8, ligne, ligne);
        }

        Finaliser(feuille, entetes.Length, ligne - 1, 3);

        return Task.FromResult(Serialiser(classeur));
    }

    public Task<byte[]> ExporterAchatsAsync(
        IReadOnlyList<ResumeAchatDto> achats,
        Periode periode,
        CancellationToken jeton = default)
    {
        using var classeur = new XLWorkbook();
        var feuille = classeur.AddWorksheet("Achats");

        EcrireTitrePeriode(feuille, "Achats fournisseurs", periode, 8);

        string[] entetes =
        [
            "N° de commande", "Date", "Fournisseur", "Articles",
            "Total", "Montant payé", "Reste à payer", "Statut"
        ];

        EcrireEntetes(feuille, entetes, 3);

        var ligne = 4;

        foreach (var achat in achats)
        {
            feuille.Cell(ligne, 1).Value = achat.NumeroAchat;
            feuille.Cell(ligne, 2).Value = achat.DateAchat.ToLocalTime();
            feuille.Cell(ligne, 2).Style.DateFormat.Format = FormatDateHeure;
            feuille.Cell(ligne, 3).Value = achat.FournisseurNom;
            feuille.Cell(ligne, 4).Value = achat.NombreArticles;
            feuille.Cell(ligne, 5).Value = achat.Total;
            feuille.Cell(ligne, 6).Value = achat.MontantPaye;
            feuille.Cell(ligne, 7).Value = achat.MontantRestant;
            feuille.Cell(ligne, 8).Value = achat.LibelleStatut;

            ligne++;
        }

        AppliquerFormatMontant(feuille, 5, 7, 4, ligne - 1);

        if (achats.Count > 0)
        {
            EcrireLigneTotal(feuille, ligne, "TOTAL", new Dictionary<int, object>
            {
                [4] = achats.Sum(a => a.NombreArticles),
                [5] = achats.Sum(a => a.Total),
                [6] = achats.Sum(a => a.MontantPaye),
                [7] = achats.Sum(a => a.MontantRestant)
            });

            AppliquerFormatMontant(feuille, 5, 7, ligne, ligne);
        }

        Finaliser(feuille, entetes.Length, ligne - 1, 3);

        return Task.FromResult(Serialiser(classeur));
    }

    public Task<byte[]> ExporterRetoursAsync(
        IReadOnlyList<ResumeRetourDto> retours,
        Periode periode,
        CancellationToken jeton = default)
    {
        using var classeur = new XLWorkbook();
        var feuille = classeur.AddWorksheet("Retours");

        EcrireTitrePeriode(feuille, "Retours clients", periode, 8);

        string[] entetes =
        [
            "N° de retour", "Vente d'origine", "Date", "Client",
            "Enregistré par", "Articles", "Montant remboursé", "Motif"
        ];

        EcrireEntetes(feuille, entetes, 3);

        var ligne = 4;

        foreach (var retour in retours)
        {
            feuille.Cell(ligne, 1).Value = retour.NumeroRetour;
            feuille.Cell(ligne, 2).Value = retour.NumeroVente;
            feuille.Cell(ligne, 3).Value = retour.DateRetour.ToLocalTime();
            feuille.Cell(ligne, 3).Style.DateFormat.Format = FormatDateHeure;
            feuille.Cell(ligne, 4).Value = retour.ClientNom ?? "Client de passage";
            feuille.Cell(ligne, 5).Value = retour.UtilisateurNom;
            feuille.Cell(ligne, 6).Value = retour.NombreArticles;
            feuille.Cell(ligne, 7).Value = retour.TotalRembourse;
            feuille.Cell(ligne, 8).Value = retour.Motif ?? string.Empty;

            ligne++;
        }

        AppliquerFormatMontant(feuille, 7, 7, 4, ligne - 1);

        if (retours.Count > 0)
        {
            EcrireLigneTotal(feuille, ligne, "TOTAL", new Dictionary<int, object>
            {
                [6] = retours.Sum(r => r.NombreArticles),
                [7] = retours.Sum(r => r.TotalRembourse)
            });

            AppliquerFormatMontant(feuille, 7, 7, ligne, ligne);
        }

        Finaliser(feuille, entetes.Length, ligne - 1, 3);

        return Task.FromResult(Serialiser(classeur));
    }

    public Task<byte[]> ExporterMouvementsStockAsync(
        IReadOnlyList<MouvementStockDto> mouvements,
        CancellationToken jeton = default)
    {
        using var classeur = new XLWorkbook();
        var feuille = classeur.AddWorksheet("Mouvements de stock");

        string[] entetes =
        [
            "Date", "Article", "Code article", "Type de mouvement", "Quantité",
            "Stock avant", "Stock après", "Motif", "Document", "Utilisateur"
        ];

        EcrireEntetes(feuille, entetes);

        var ligne = 2;

        foreach (var mouvement in mouvements)
        {
            feuille.Cell(ligne, 1).Value = mouvement.DateCreation.ToLocalTime();
            feuille.Cell(ligne, 1).Style.DateFormat.Format = FormatDateHeure;
            feuille.Cell(ligne, 2).Value = mouvement.Designation;
            feuille.Cell(ligne, 3).Value = mouvement.Sku;
            feuille.Cell(ligne, 4).Value = mouvement.LibelleTypeMouvement;
            feuille.Cell(ligne, 5).Value = mouvement.Quantite;
            feuille.Cell(ligne, 6).Value = mouvement.AncienneQuantite;
            feuille.Cell(ligne, 7).Value = mouvement.NouvelleQuantite;
            feuille.Cell(ligne, 8).Value = mouvement.Motif ?? string.Empty;
            feuille.Cell(ligne, 9).Value = mouvement.ReferenceDocument ?? string.Empty;
            feuille.Cell(ligne, 10).Value = mouvement.Utilisateur ?? string.Empty;

            // Les entrées apparaissent en vert, les sorties en rouge.
            feuille.Cell(ligne, 5).Style.Font.FontColor = mouvement.EstEntree
                ? XLColor.FromHtml("#1E7E34")
                : XLColor.FromHtml("#C0392B");

            feuille.Cell(ligne, 5).Style.Font.Bold = true;

            ligne++;
        }

        Finaliser(feuille, entetes.Length, ligne - 1);

        return Task.FromResult(Serialiser(classeur));
    }

    public Task<byte[]> ExporterRapportActiviteAsync(
        Periode periode,
        SyntheseActiviteDto synthese,
        IReadOnlyList<ClassementArticleDto> meilleuresVentes,
        IReadOnlyList<ClassementArticleDto> moinsBonnesVentes,
        IReadOnlyList<VentesParEmployeDto> ventesParEmploye,
        IReadOnlyList<VentesParModePaiementDto> repartitionPaiements,
        CancellationToken jeton = default)
    {
        using var classeur = new XLWorkbook();

        // --- Synthèse ---
        var feuilleSynthese = classeur.AddWorksheet("Synthèse");

        EcrireTitrePeriode(feuilleSynthese, "Rapport d'activité", periode, 2);

        var indicateurs = new (string Libelle, object Valeur, bool EstMontant)[]
        {
            ("Chiffre d'affaires net", synthese.ChiffreAffaires, true),
            ("Chiffre d'affaires brut", synthese.ChiffreAffairesBrut, true),
            ("Montant remboursé", synthese.MontantRetourne, true),
            ("Bénéfice estimé", synthese.BeneficeEstime, true),
            ("Taux de marge (%)", synthese.TauxMarge, false),
            ("Nombre de ventes", synthese.NombreVentes, false),
            ("Articles vendus", synthese.ArticlesVendus, false),
            ("Ticket moyen", synthese.TicketMoyen, true),
            ("Nombre de retours", synthese.NombreRetours, false),
            ("Total des achats", synthese.TotalAchats, true)
        };

        var ligne = 3;

        foreach (var (libelle, valeur, estMontant) in indicateurs)
        {
            feuilleSynthese.Cell(ligne, 1).Value = libelle;
            feuilleSynthese.Cell(ligne, 1).Style.Font.Bold = true;

            switch (valeur)
            {
                case decimal montant:
                    feuilleSynthese.Cell(ligne, 2).Value = montant;
                    break;
                case int nombre:
                    feuilleSynthese.Cell(ligne, 2).Value = nombre;
                    break;
                default:
                    feuilleSynthese.Cell(ligne, 2).Value = valeur.ToString();
                    break;
            }

            if (estMontant)
            {
                feuilleSynthese.Cell(ligne, 2).Style.NumberFormat.Format = FormatMontant;
            }

            ligne++;
        }

        feuilleSynthese.Columns(1, 2).AdjustToContents();

        // --- Classements ---
        EcrireFeuilleClassement(classeur, "Meilleures ventes", meilleuresVentes);
        EcrireFeuilleClassement(classeur, "Articles les moins vendus", moinsBonnesVentes);

        // --- Ventes par employé ---
        var feuilleEmployes = classeur.AddWorksheet("Ventes par employé");

        string[] entetesEmployes =
            ["Employé", "Rôle", "Ventes", "Articles vendus", "Chiffre d'affaires", "Ticket moyen"];

        EcrireEntetes(feuilleEmployes, entetesEmployes);

        ligne = 2;

        foreach (var employe in ventesParEmploye)
        {
            feuilleEmployes.Cell(ligne, 1).Value = employe.Utilisateur;
            feuilleEmployes.Cell(ligne, 2).Value = employe.Role;
            feuilleEmployes.Cell(ligne, 3).Value = employe.NombreVentes;
            feuilleEmployes.Cell(ligne, 4).Value = employe.ArticlesVendus;
            feuilleEmployes.Cell(ligne, 5).Value = employe.ChiffreAffaires;
            feuilleEmployes.Cell(ligne, 6).Value = employe.TicketMoyen;

            ligne++;
        }

        AppliquerFormatMontant(feuilleEmployes, 5, 6, 2, ligne - 1);
        Finaliser(feuilleEmployes, entetesEmployes.Length, ligne - 1);

        // --- Modes de paiement ---
        var feuillePaiements = classeur.AddWorksheet("Modes de paiement");

        string[] entetesPaiements = ["Mode de paiement", "Nombre de règlements", "Montant", "Part (%)"];

        EcrireEntetes(feuillePaiements, entetesPaiements);

        ligne = 2;

        foreach (var mode in repartitionPaiements)
        {
            feuillePaiements.Cell(ligne, 1).Value = mode.ModePaiement;
            feuillePaiements.Cell(ligne, 2).Value = mode.NombreReglements;
            feuillePaiements.Cell(ligne, 3).Value = mode.Montant;
            feuillePaiements.Cell(ligne, 4).Value = mode.Pourcentage;

            ligne++;
        }

        AppliquerFormatMontant(feuillePaiements, 3, 3, 2, ligne - 1);
        Finaliser(feuillePaiements, entetesPaiements.Length, ligne - 1);

        return Task.FromResult(Serialiser(classeur));
    }

    // ==================================================================
    // Utilitaires de mise en forme
    // ==================================================================

    private static void EcrireFeuilleClassement(
        XLWorkbook classeur,
        string nomFeuille,
        IReadOnlyList<ClassementArticleDto> articles)
    {
        var feuille = classeur.AddWorksheet(nomFeuille);

        string[] entetes =
        [
            "Rang", "Article", "Code article", "Marque",
            "Quantité vendue", "Chiffre d'affaires", "Bénéfice", "Stock actuel"
        ];

        EcrireEntetes(feuille, entetes);

        var ligne = 2;
        var rang = 1;

        foreach (var article in articles)
        {
            feuille.Cell(ligne, 1).Value = rang++;
            feuille.Cell(ligne, 2).Value = article.Designation;
            feuille.Cell(ligne, 3).Value = article.Sku;
            feuille.Cell(ligne, 4).Value = article.Marque ?? string.Empty;
            feuille.Cell(ligne, 5).Value = article.QuantiteVendue;
            feuille.Cell(ligne, 6).Value = article.ChiffreAffaires;
            feuille.Cell(ligne, 7).Value = article.Benefice;
            feuille.Cell(ligne, 8).Value = article.StockActuel;

            ligne++;
        }

        AppliquerFormatMontant(feuille, 6, 7, 2, ligne - 1);
        Finaliser(feuille, entetes.Length, ligne - 1);
    }

    private static void EcrireEntetes(IXLWorksheet feuille, string[] entetes, int ligne = 1)
    {
        for (var colonne = 0; colonne < entetes.Length; colonne++)
        {
            var cellule = feuille.Cell(ligne, colonne + 1);

            cellule.Value = entetes[colonne];
            cellule.Style.Font.Bold = true;
            cellule.Style.Font.FontColor = XLColor.White;
            cellule.Style.Fill.BackgroundColor = CouleurEntete;
            cellule.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cellule.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        feuille.Row(ligne).Height = 20;
    }

    private static void EcrireTitrePeriode(
        IXLWorksheet feuille,
        string titre,
        Periode periode,
        int nombreColonnes)
    {
        var cellule = feuille.Cell(1, 1);

        cellule.Value = $"{titre} — {periode.Libelle}";
        cellule.Style.Font.Bold = true;
        cellule.Style.Font.FontSize = 14;
        cellule.Style.Font.FontColor = CouleurEntete;

        if (nombreColonnes > 1)
        {
            feuille.Range(1, 1, 1, nombreColonnes).Merge();
        }
    }

    private static void EcrireLigneTotal(
        IXLWorksheet feuille,
        int ligne,
        string libelle,
        Dictionary<int, object> valeurs)
    {
        feuille.Cell(ligne, 1).Value = libelle;

        foreach (var (colonne, valeur) in valeurs)
        {
            switch (valeur)
            {
                case decimal montant:
                    feuille.Cell(ligne, colonne).Value = montant;
                    break;
                case int nombre:
                    feuille.Cell(ligne, colonne).Value = nombre;
                    break;
                default:
                    feuille.Cell(ligne, colonne).Value = valeur.ToString();
                    break;
            }
        }

        var derniereColonne = valeurs.Count > 0 ? valeurs.Keys.Max() : 1;
        var plage = feuille.Range(ligne, 1, ligne, derniereColonne);

        plage.Style.Font.Bold = true;
        plage.Style.Fill.BackgroundColor = CouleurTotal;
        plage.Style.Border.TopBorder = XLBorderStyleValues.Thin;
    }

    private static void AppliquerFormatMontant(
        IXLWorksheet feuille,
        int premiereColonne,
        int derniereColonne,
        int premiereLigne,
        int derniereLigne)
    {
        if (derniereLigne < premiereLigne)
        {
            return;
        }

        feuille.Range(premiereLigne, premiereColonne, derniereLigne, derniereColonne)
            .Style.NumberFormat.Format = FormatMontant;
    }

    private static void Finaliser(
        IXLWorksheet feuille,
        int nombreColonnes,
        int derniereLigne,
        int ligneEntete = 1)
    {
        if (derniereLigne >= ligneEntete)
        {
            // Le filtre automatique et le figement de l'en-tête rendent le
            // classeur immédiatement exploitable par le magasin.
            feuille.Range(ligneEntete, 1, Math.Max(derniereLigne, ligneEntete), nombreColonnes)
                .SetAutoFilter();
        }

        feuille.SheetView.FreezeRows(ligneEntete);
        feuille.Columns(1, nombreColonnes).AdjustToContents();

        // Une colonne trop large rend la lecture pénible : la largeur est bornée.
        foreach (var colonne in feuille.Columns(1, nombreColonnes))
        {
            if (colonne.Width > 42)
            {
                colonne.Width = 42;
            }
        }
    }

    private static byte[] Serialiser(XLWorkbook classeur)
    {
        using var flux = new MemoryStream();

        classeur.SaveAs(flux);

        return flux.ToArray();
    }
}
