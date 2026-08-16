using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionMagasin.Infrastructure.Documents;

/// <summary>
/// Génération des documents PDF avec QuestPDF. Tous les libellés sont en
/// français et les montants suivent la devise configurée dans les paramètres
/// du magasin.
/// </summary>
public class ServiceDocumentsPdf : IServiceDocumentsPdf
{
    private readonly IServiceParametres _parametres;

    public ServiceDocumentsPdf(IServiceParametres parametres)
    {
        _parametres = parametres;
    }

    // ==================================================================
    // Ticket de caisse
    // ==================================================================

    public async Task<byte[]> GenererTicketAsync(VenteDto vente, CancellationToken jeton = default)
    {
        var magasin = await _parametres.ObtenirAsync(jeton).ConfigureAwait(false);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(StyleDocuments.FormatTicket);
                page.ContinuousSize(80, Unit.Millimetre);
                page.Margin(5, Unit.Millimetre);
                page.DefaultTextStyle(t => t
                    .FontSize(StyleDocuments.TaillePoliceTicket)
                    .FontColor(StyleDocuments.CouleurTexte));

                page.Content().Column(colonne =>
                {
                    colonne.Spacing(4);

                    // En-tête du magasin
                    colonne.Item().AlignCenter().Text(magasin.NomMagasin)
                        .Bold().FontSize(12).FontColor(StyleDocuments.CouleurPrincipale);

                    if (!string.IsNullOrWhiteSpace(magasin.Adresse))
                    {
                        colonne.Item().AlignCenter().Text(magasin.Adresse).FontSize(7.5f);
                    }

                    if (!string.IsNullOrWhiteSpace(magasin.Telephone))
                    {
                        colonne.Item().AlignCenter().Text($"Tél. : {magasin.Telephone}").FontSize(7.5f);
                    }

                    if (!string.IsNullOrWhiteSpace(magasin.RegistreCommerce))
                    {
                        colonne.Item().AlignCenter().Text($"RC : {magasin.RegistreCommerce}").FontSize(7f);
                    }

                    colonne.Item().PaddingVertical(3).LineHorizontal(0.7f)
                        .LineColor(StyleDocuments.CouleurBordure);

                    // Informations de la vente
                    colonne.Item().Text($"Ticket n° {vente.NumeroVente}").Bold();
                    colonne.Item().Text($"Date : {FormatageMontant.FormaterDateHeure(vente.DateVente)}");
                    colonne.Item().Text($"Caissier : {vente.UtilisateurNom}");

                    if (!string.IsNullOrWhiteSpace(vente.ClientNom))
                    {
                        colonne.Item().Text($"Client : {vente.ClientNom}");
                    }

                    colonne.Item().PaddingVertical(3).LineHorizontal(0.7f)
                        .LineColor(StyleDocuments.CouleurBordure);

                    // Articles
                    foreach (var ligne in vente.Lignes)
                    {
                        colonne.Item().Column(article =>
                        {
                            article.Item().Text(ligne.Designation).Bold().FontSize(8);

                            article.Item().Row(details =>
                            {
                                details.RelativeItem().Text(
                                    $"{ligne.Quantite} × {FormatageMontant.FormaterSansDevise(ligne.PrixUnitaire)}");

                                details.ConstantItem(60).AlignRight().Text(
                                    FormatageMontant.Formater(ligne.Total, magasin.SymboleDevise));
                            });

                            if (ligne.Remise > 0)
                            {
                                article.Item().Text(
                                        $"  Remise : -{FormatageMontant.Formater(ligne.Remise, magasin.SymboleDevise)}")
                                    .FontSize(7.5f).FontColor(StyleDocuments.CouleurSucces);
                            }
                        });
                    }

                    colonne.Item().PaddingVertical(3).LineHorizontal(0.7f)
                        .LineColor(StyleDocuments.CouleurBordure);

                    // Totaux
                    LigneTotal(colonne, "Sous-total", vente.SousTotal, magasin.SymboleDevise);

                    if (vente.Remise > 0)
                    {
                        LigneTotal(colonne, "Remise", -vente.Remise, magasin.SymboleDevise);
                    }

                    colonne.Item().PaddingTop(2).Row(ligne =>
                    {
                        ligne.RelativeItem().Text("TOTAL").Bold().FontSize(11);
                        ligne.ConstantItem(80).AlignRight()
                            .Text(FormatageMontant.Formater(vente.Total, magasin.SymboleDevise))
                            .Bold().FontSize(11).FontColor(StyleDocuments.CouleurPrincipale);
                    });

                    colonne.Item().PaddingTop(3);

                    foreach (var paiement in vente.Paiements)
                    {
                        LigneTotal(colonne, paiement.LibelleModePaiement, paiement.Montant, magasin.SymboleDevise);
                    }

                    if (vente.MontantRestant > 0)
                    {
                        colonne.Item().PaddingTop(2).Row(ligne =>
                        {
                            ligne.RelativeItem().Text("Reste à payer").Bold()
                                .FontColor(StyleDocuments.CouleurAlerte);
                            ligne.ConstantItem(70).AlignRight()
                                .Text(FormatageMontant.Formater(vente.MontantRestant, magasin.SymboleDevise))
                                .Bold().FontColor(StyleDocuments.CouleurAlerte);
                        });
                    }

                    colonne.Item().PaddingTop(4).AlignCenter()
                        .Text($"Nombre d'articles : {vente.NombreArticles}").FontSize(8);

                    colonne.Item().PaddingVertical(3).LineHorizontal(0.7f)
                        .LineColor(StyleDocuments.CouleurBordure);

                    if (!string.IsNullOrWhiteSpace(magasin.InformationsTicket))
                    {
                        colonne.Item().AlignCenter().Text(magasin.InformationsTicket).FontSize(7.5f).Italic();
                    }

                    colonne.Item().PaddingTop(2).AlignCenter()
                        .Text($"Échange ou retour sous {magasin.DelaiRetourJours} jours sur présentation du ticket.")
                        .FontSize(6.5f).FontColor(StyleDocuments.CouleurTexteAttenue);
                });
            });
        }).GeneratePdf();
    }

    // ==================================================================
    // Facture
    // ==================================================================

    public async Task<byte[]> GenererFactureAsync(VenteDto vente, CancellationToken jeton = default)
    {
        var magasin = await _parametres.ObtenirAsync(jeton).ConfigureAwait(false);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                ConfigurerPageA4(page);

                page.Header().Element(entete => EnteteDocument(
                    entete, magasin, "FACTURE", vente.NumeroVente, vente.DateVente));

                page.Content().PaddingVertical(12).Column(colonne =>
                {
                    colonne.Spacing(10);

                    // Bloc client
                    colonne.Item().Row(ligne =>
                    {
                        ligne.RelativeItem().Element(bloc => bloc
                            .Background(StyleDocuments.CouleurFondEntete)
                            .Padding(8)
                            .Column(client =>
                            {
                                client.Item().Text("Client").Bold()
                                    .FontColor(StyleDocuments.CouleurPrincipale);

                                client.Item().Text(string.IsNullOrWhiteSpace(vente.ClientNom)
                                    ? "Client de passage"
                                    : vente.ClientNom);

                                if (!string.IsNullOrWhiteSpace(vente.ClientTelephone))
                                {
                                    client.Item().Text($"Tél. : {vente.ClientTelephone}");
                                }
                            }));

                        ligne.ConstantItem(15);

                        ligne.RelativeItem().Element(bloc => bloc
                            .Background(StyleDocuments.CouleurFondEntete)
                            .Padding(8)
                            .Column(infos =>
                            {
                                infos.Item().Text("Informations").Bold()
                                    .FontColor(StyleDocuments.CouleurPrincipale);
                                infos.Item().Text($"Vendeur : {vente.UtilisateurNom}");
                                infos.Item().Text($"Statut : {vente.LibelleStatut}");
                            }));
                    });

                    // Tableau des articles
                    colonne.Item().Table(tableau =>
                    {
                        tableau.ColumnsDefinition(colonnes =>
                        {
                            colonnes.RelativeColumn(4);
                            colonnes.RelativeColumn(1.4f);
                            colonnes.ConstantColumn(45);
                            colonnes.ConstantColumn(75);
                            colonnes.ConstantColumn(65);
                            colonnes.ConstantColumn(80);
                        });

                        tableau.Header(entete =>
                        {
                            CelluleEntete(entete, "Désignation");
                            CelluleEntete(entete, "Code article");
                            CelluleEntete(entete, "Qté", true);
                            CelluleEntete(entete, "Prix unitaire", true);
                            CelluleEntete(entete, "Remise", true);
                            CelluleEntete(entete, "Total", true);
                        });

                        var index = 0;

                        foreach (var article in vente.Lignes)
                        {
                            var fond = index++ % 2 == 0 ? Colors.White : StyleDocuments.CouleurFondAlterne;

                            Cellule(tableau, article.Designation, fond);
                            Cellule(tableau, article.Sku, fond);
                            Cellule(tableau, article.Quantite.ToString(), fond, true);
                            Cellule(tableau, FormatageMontant.FormaterSansDevise(article.PrixUnitaire), fond, true);
                            Cellule(tableau, article.Remise > 0
                                ? FormatageMontant.FormaterSansDevise(article.Remise)
                                : "—", fond, true);
                            Cellule(tableau, FormatageMontant.FormaterSansDevise(article.Total), fond, true);
                        }
                    });

                    // Totaux
                    colonne.Item().AlignRight().Width(240).Column(totaux =>
                    {
                        LigneTotalFacture(totaux, "Sous-total", vente.SousTotal, magasin.SymboleDevise);

                        if (vente.Remise > 0)
                        {
                            LigneTotalFacture(totaux, "Remise", -vente.Remise, magasin.SymboleDevise);
                        }

                        totaux.Item().PaddingVertical(3).LineHorizontal(1)
                            .LineColor(StyleDocuments.CouleurPrincipale);

                        totaux.Item().Row(ligne =>
                        {
                            ligne.RelativeItem().Text("Total à payer").Bold().FontSize(12);
                            ligne.ConstantItem(110).AlignRight()
                                .Text(FormatageMontant.Formater(vente.Total, magasin.SymboleDevise))
                                .Bold().FontSize(12).FontColor(StyleDocuments.CouleurPrincipale);
                        });

                        totaux.Item().PaddingTop(5);

                        foreach (var paiement in vente.Paiements)
                        {
                            LigneTotalFacture(
                                totaux,
                                $"Réglé — {paiement.LibelleModePaiement}",
                                paiement.Montant,
                                magasin.SymboleDevise);
                        }

                        if (vente.MontantRestant > 0)
                        {
                            totaux.Item().Row(ligne =>
                            {
                                ligne.RelativeItem().Text("Reste à payer").Bold()
                                    .FontColor(StyleDocuments.CouleurAlerte);
                                ligne.ConstantItem(110).AlignRight()
                                    .Text(FormatageMontant.Formater(vente.MontantRestant, magasin.SymboleDevise))
                                    .Bold().FontColor(StyleDocuments.CouleurAlerte);
                            });
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(vente.Notes))
                    {
                        colonne.Item().PaddingTop(8).Text($"Observations : {vente.Notes}")
                            .FontSize(8.5f).FontColor(StyleDocuments.CouleurTexteAttenue);
                    }
                });

                page.Footer().Element(pied => PiedDePage(pied, magasin));
            });
        }).GeneratePdf();
    }

    // ==================================================================
    // Bon de commande fournisseur
    // ==================================================================

    public async Task<byte[]> GenererBonCommandeAsync(AchatDto achat, CancellationToken jeton = default)
    {
        var magasin = await _parametres.ObtenirAsync(jeton).ConfigureAwait(false);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                ConfigurerPageA4(page);

                page.Header().Element(entete => EnteteDocument(
                    entete, magasin, "BON DE COMMANDE", achat.NumeroAchat, achat.DateAchat));

                page.Content().PaddingVertical(12).Column(colonne =>
                {
                    colonne.Spacing(10);

                    colonne.Item().Element(bloc => bloc
                        .Background(StyleDocuments.CouleurFondEntete)
                        .Padding(8)
                        .Column(fournisseur =>
                        {
                            fournisseur.Item().Text("Fournisseur").Bold()
                                .FontColor(StyleDocuments.CouleurPrincipale);
                            fournisseur.Item().Text(achat.FournisseurNom);

                            if (!string.IsNullOrWhiteSpace(achat.FournisseurTelephone))
                            {
                                fournisseur.Item().Text($"Tél. : {achat.FournisseurTelephone}");
                            }

                            fournisseur.Item().Text($"Statut : {achat.LibelleStatut}");
                        }));

                    colonne.Item().Table(tableau =>
                    {
                        tableau.ColumnsDefinition(colonnes =>
                        {
                            colonnes.RelativeColumn(4);
                            colonnes.RelativeColumn(1.6f);
                            colonnes.ConstantColumn(55);
                            colonnes.ConstantColumn(55);
                            colonnes.ConstantColumn(80);
                            colonnes.ConstantColumn(80);
                        });

                        tableau.Header(entete =>
                        {
                            CelluleEntete(entete, "Désignation");
                            CelluleEntete(entete, "Code article");
                            CelluleEntete(entete, "Commandé", true);
                            CelluleEntete(entete, "Reçu", true);
                            CelluleEntete(entete, "Prix unitaire", true);
                            CelluleEntete(entete, "Total", true);
                        });

                        var index = 0;

                        foreach (var ligne in achat.Lignes)
                        {
                            var fond = index++ % 2 == 0 ? Colors.White : StyleDocuments.CouleurFondAlterne;

                            Cellule(tableau, ligne.Designation, fond);
                            Cellule(tableau, ligne.Sku, fond);
                            Cellule(tableau, ligne.Quantite.ToString(), fond, true);
                            Cellule(tableau, ligne.QuantiteRecue.ToString(), fond, true);
                            Cellule(tableau, FormatageMontant.FormaterSansDevise(ligne.PrixUnitaire), fond, true);
                            Cellule(tableau, FormatageMontant.FormaterSansDevise(ligne.Total), fond, true);
                        }
                    });

                    colonne.Item().AlignRight().Width(250).Column(totaux =>
                    {
                        LigneTotalFacture(totaux, "Sous-total", achat.SousTotal, magasin.SymboleDevise);

                        if (achat.Remise > 0)
                        {
                            LigneTotalFacture(totaux, "Remise", -achat.Remise, magasin.SymboleDevise);
                        }

                        totaux.Item().PaddingVertical(3).LineHorizontal(1)
                            .LineColor(StyleDocuments.CouleurPrincipale);

                        totaux.Item().Row(ligne =>
                        {
                            ligne.RelativeItem().Text("Total").Bold().FontSize(12);
                            ligne.ConstantItem(110).AlignRight()
                                .Text(FormatageMontant.Formater(achat.Total, magasin.SymboleDevise))
                                .Bold().FontSize(12).FontColor(StyleDocuments.CouleurPrincipale);
                        });

                        LigneTotalFacture(totaux, "Déjà réglé", achat.MontantPaye, magasin.SymboleDevise);

                        if (achat.MontantRestant > 0)
                        {
                            totaux.Item().Row(ligne =>
                            {
                                ligne.RelativeItem().Text("Reste à payer").Bold()
                                    .FontColor(StyleDocuments.CouleurAlerte);
                                ligne.ConstantItem(110).AlignRight()
                                    .Text(FormatageMontant.Formater(achat.MontantRestant, magasin.SymboleDevise))
                                    .Bold().FontColor(StyleDocuments.CouleurAlerte);
                            });
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(achat.Notes))
                    {
                        colonne.Item().PaddingTop(8).Text($"Observations : {achat.Notes}")
                            .FontSize(8.5f).FontColor(StyleDocuments.CouleurTexteAttenue);
                    }
                });

                page.Footer().Element(pied => PiedDePage(pied, magasin));
            });
        }).GeneratePdf();
    }

    // ==================================================================
    // Bon de retour
    // ==================================================================

    public async Task<byte[]> GenererBonRetourAsync(RetourDto retour, CancellationToken jeton = default)
    {
        var magasin = await _parametres.ObtenirAsync(jeton).ConfigureAwait(false);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                ConfigurerPageA4(page);

                page.Header().Element(entete => EnteteDocument(
                    entete, magasin, "BON DE RETOUR", retour.NumeroRetour, retour.DateRetour));

                page.Content().PaddingVertical(12).Column(colonne =>
                {
                    colonne.Spacing(10);

                    colonne.Item().Element(bloc => bloc
                        .Background(StyleDocuments.CouleurFondEntete)
                        .Padding(8)
                        .Column(infos =>
                        {
                            infos.Item().Text($"Vente d'origine : {retour.NumeroVente}").Bold();
                            infos.Item().Text($"Date de la vente : {FormatageMontant.FormaterDate(retour.DateVente)}");
                            infos.Item().Text($"Client : {retour.ClientNom ?? "Client de passage"}");
                            infos.Item().Text($"Enregistré par : {retour.UtilisateurNom}");

                            if (!string.IsNullOrWhiteSpace(retour.Motif))
                            {
                                infos.Item().Text($"Motif : {retour.Motif}");
                            }
                        }));

                    colonne.Item().Table(tableau =>
                    {
                        tableau.ColumnsDefinition(colonnes =>
                        {
                            colonnes.RelativeColumn(4);
                            colonnes.RelativeColumn(1.6f);
                            colonnes.ConstantColumn(45);
                            colonnes.ConstantColumn(90);
                            colonnes.ConstantColumn(90);
                            colonnes.ConstantColumn(85);
                        });

                        tableau.Header(entete =>
                        {
                            CelluleEntete(entete, "Désignation");
                            CelluleEntete(entete, "Code article");
                            CelluleEntete(entete, "Qté", true);
                            CelluleEntete(entete, "État");
                            CelluleEntete(entete, "Remis en stock");
                            CelluleEntete(entete, "Remboursé", true);
                        });

                        var index = 0;

                        foreach (var ligne in retour.Lignes)
                        {
                            var fond = index++ % 2 == 0 ? Colors.White : StyleDocuments.CouleurFondAlterne;

                            Cellule(tableau, ligne.Designation, fond);
                            Cellule(tableau, ligne.Sku, fond);
                            Cellule(tableau, ligne.Quantite.ToString(), fond, true);
                            Cellule(tableau, ligne.LibelleEtatArticle, fond);
                            Cellule(tableau, ligne.RemisEnStock ? "Oui" : "Non", fond);
                            Cellule(tableau, FormatageMontant.FormaterSansDevise(ligne.MontantRembourse), fond, true);
                        }
                    });

                    colonne.Item().AlignRight().Width(250).Column(totaux =>
                    {
                        totaux.Item().PaddingVertical(3).LineHorizontal(1)
                            .LineColor(StyleDocuments.CouleurPrincipale);

                        totaux.Item().Row(ligne =>
                        {
                            ligne.RelativeItem().Text("Total remboursé").Bold().FontSize(12);
                            ligne.ConstantItem(110).AlignRight()
                                .Text(FormatageMontant.Formater(retour.TotalRembourse, magasin.SymboleDevise))
                                .Bold().FontSize(12).FontColor(StyleDocuments.CouleurPrincipale);
                        });
                    });

                    colonne.Item().PaddingTop(25).Row(signatures =>
                    {
                        signatures.RelativeItem().Column(bloc =>
                        {
                            bloc.Item().Text("Signature du client").FontSize(8.5f);
                            bloc.Item().PaddingTop(28).LineHorizontal(0.6f)
                                .LineColor(StyleDocuments.CouleurBordure);
                        });

                        signatures.ConstantItem(40);

                        signatures.RelativeItem().Column(bloc =>
                        {
                            bloc.Item().Text("Signature du magasin").FontSize(8.5f);
                            bloc.Item().PaddingTop(28).LineHorizontal(0.6f)
                                .LineColor(StyleDocuments.CouleurBordure);
                        });
                    });
                });

                page.Footer().Element(pied => PiedDePage(pied, magasin));
            });
        }).GeneratePdf();
    }

    // ==================================================================
    // Rapports
    // ==================================================================

    public async Task<byte[]> GenererRapportActiviteAsync(
        Periode periode,
        SyntheseActiviteDto synthese,
        IReadOnlyList<ClassementArticleDto> meilleuresVentes,
        IReadOnlyList<VentesParEmployeDto> ventesParEmploye,
        IReadOnlyList<VentesParModePaiementDto> repartitionPaiements,
        CancellationToken jeton = default)
    {
        var magasin = await _parametres.ObtenirAsync(jeton).ConfigureAwait(false);
        var devise = magasin.SymboleDevise;

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                ConfigurerPageA4(page);

                page.Header().Element(entete => EnteteRapport(
                    entete, magasin, "RAPPORT D'ACTIVITÉ", periode.Libelle));

                page.Content().PaddingVertical(12).Column(colonne =>
                {
                    colonne.Spacing(14);

                    // Indicateurs clés
                    colonne.Item().Row(ligne =>
                    {
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Chiffre d'affaires", FormatageMontant.Formater(synthese.ChiffreAffaires, devise)));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Bénéfice estimé", FormatageMontant.Formater(synthese.BeneficeEstime, devise)));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Ventes", synthese.NombreVentes.ToString()));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Articles vendus", synthese.ArticlesVendus.ToString()));
                    });

                    colonne.Item().Row(ligne =>
                    {
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Ticket moyen", FormatageMontant.Formater(synthese.TicketMoyen, devise)));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Taux de marge", $"{FormatageMontant.FormaterSansDevise(synthese.TauxMarge)} %"));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Retours", $"{synthese.NombreRetours} ({FormatageMontant.Formater(synthese.MontantRetourne, devise)})"));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Achats", FormatageMontant.Formater(synthese.TotalAchats, devise)));
                    });

                    // Meilleures ventes
                    if (meilleuresVentes.Count > 0)
                    {
                        colonne.Item().Element(c => TitreSection(c, "Articles les plus vendus"));

                        colonne.Item().Table(tableau =>
                        {
                            tableau.ColumnsDefinition(colonnes =>
                            {
                                colonnes.ConstantColumn(25);
                                colonnes.RelativeColumn(4);
                                colonnes.RelativeColumn(1.5f);
                                colonnes.ConstantColumn(60);
                                colonnes.ConstantColumn(85);
                                colonnes.ConstantColumn(85);
                            });

                            tableau.Header(entete =>
                            {
                                CelluleEntete(entete, "N°", true);
                                CelluleEntete(entete, "Article");
                                CelluleEntete(entete, "Code article");
                                CelluleEntete(entete, "Vendus", true);
                                CelluleEntete(entete, "Chiffre d'affaires", true);
                                CelluleEntete(entete, "Bénéfice", true);
                            });

                            var rang = 1;

                            foreach (var article in meilleuresVentes)
                            {
                                var fond = rang % 2 == 1 ? Colors.White : StyleDocuments.CouleurFondAlterne;

                                Cellule(tableau, rang.ToString(), fond, true);
                                Cellule(tableau, article.Designation, fond);
                                Cellule(tableau, article.Sku, fond);
                                Cellule(tableau, article.QuantiteVendue.ToString(), fond, true);
                                Cellule(tableau, FormatageMontant.FormaterSansDevise(article.ChiffreAffaires), fond, true);
                                Cellule(tableau, FormatageMontant.FormaterSansDevise(article.Benefice), fond, true);

                                rang++;
                            }
                        });
                    }

                    // Ventes par employé
                    if (ventesParEmploye.Count > 0)
                    {
                        colonne.Item().Element(c => TitreSection(c, "Ventes par employé"));

                        colonne.Item().Table(tableau =>
                        {
                            tableau.ColumnsDefinition(colonnes =>
                            {
                                colonnes.RelativeColumn(3);
                                colonnes.RelativeColumn(2);
                                colonnes.ConstantColumn(60);
                                colonnes.ConstantColumn(65);
                                colonnes.ConstantColumn(90);
                                colonnes.ConstantColumn(85);
                            });

                            tableau.Header(entete =>
                            {
                                CelluleEntete(entete, "Employé");
                                CelluleEntete(entete, "Rôle");
                                CelluleEntete(entete, "Ventes", true);
                                CelluleEntete(entete, "Articles", true);
                                CelluleEntete(entete, "Chiffre d'affaires", true);
                                CelluleEntete(entete, "Ticket moyen", true);
                            });

                            var index = 0;

                            foreach (var employe in ventesParEmploye)
                            {
                                var fond = index++ % 2 == 0 ? Colors.White : StyleDocuments.CouleurFondAlterne;

                                Cellule(tableau, employe.Utilisateur, fond);
                                Cellule(tableau, employe.Role, fond);
                                Cellule(tableau, employe.NombreVentes.ToString(), fond, true);
                                Cellule(tableau, employe.ArticlesVendus.ToString(), fond, true);
                                Cellule(tableau, FormatageMontant.FormaterSansDevise(employe.ChiffreAffaires), fond, true);
                                Cellule(tableau, FormatageMontant.FormaterSansDevise(employe.TicketMoyen), fond, true);
                            }
                        });
                    }

                    // Modes de règlement
                    if (repartitionPaiements.Count > 0)
                    {
                        colonne.Item().Element(c => TitreSection(c, "Répartition par mode de paiement"));

                        colonne.Item().Table(tableau =>
                        {
                            tableau.ColumnsDefinition(colonnes =>
                            {
                                colonnes.RelativeColumn(3);
                                colonnes.ConstantColumn(90);
                                colonnes.ConstantColumn(100);
                                colonnes.ConstantColumn(80);
                            });

                            tableau.Header(entete =>
                            {
                                CelluleEntete(entete, "Mode de paiement");
                                CelluleEntete(entete, "Règlements", true);
                                CelluleEntete(entete, "Montant", true);
                                CelluleEntete(entete, "Part", true);
                            });

                            var index = 0;

                            foreach (var mode in repartitionPaiements)
                            {
                                var fond = index++ % 2 == 0 ? Colors.White : StyleDocuments.CouleurFondAlterne;

                                Cellule(tableau, mode.ModePaiement, fond);
                                Cellule(tableau, mode.NombreReglements.ToString(), fond, true);
                                Cellule(tableau, FormatageMontant.FormaterSansDevise(mode.Montant), fond, true);
                                Cellule(tableau, $"{FormatageMontant.FormaterSansDevise(mode.Pourcentage)} %", fond, true);
                            }
                        });
                    }
                });

                page.Footer().Element(pied => PiedDePage(pied, magasin));
            });
        }).GeneratePdf();
    }

    public async Task<byte[]> GenererRapportStockAsync(
        IReadOnlyList<LigneStockDto> lignes,
        SyntheseStockDto synthese,
        CancellationToken jeton = default)
    {
        var magasin = await _parametres.ObtenirAsync(jeton).ConfigureAwait(false);
        var devise = magasin.SymboleDevise;

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                ConfigurerPageA4(page);
                page.Size(PageSizes.A4.Landscape());

                page.Header().Element(entete => EnteteRapport(
                    entete,
                    magasin,
                    "ÉTAT DU STOCK",
                    $"Situation au {FormatageMontant.FormaterDateHeure(DateTime.UtcNow)}"));

                page.Content().PaddingVertical(12).Column(colonne =>
                {
                    colonne.Spacing(14);

                    colonne.Item().Row(ligne =>
                    {
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Déclinaisons", synthese.NombreDeclinaisons.ToString()));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Quantité totale", synthese.QuantiteTotale.ToString()));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Valeur d'achat", FormatageMontant.Formater(synthese.ValeurAuPrixAchat, devise)));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Stock faible", synthese.NombreStockFaible.ToString()));
                        ligne.ConstantItem(8);
                        ligne.RelativeItem().Element(c => Indicateur(
                            c, "Ruptures", synthese.NombreRuptures.ToString()));
                    });

                    colonne.Item().Table(tableau =>
                    {
                        tableau.ColumnsDefinition(colonnes =>
                        {
                            colonnes.RelativeColumn(3);
                            colonnes.RelativeColumn(1.4f);
                            colonnes.RelativeColumn(1.2f);
                            colonnes.ConstantColumn(55);
                            colonnes.ConstantColumn(45);
                            colonnes.ConstantColumn(45);
                            colonnes.ConstantColumn(75);
                            colonnes.ConstantColumn(75);
                            colonnes.ConstantColumn(75);
                        });

                        tableau.Header(entete =>
                        {
                            CelluleEntete(entete, "Produit");
                            CelluleEntete(entete, "Code article");
                            CelluleEntete(entete, "Couleur / Taille");
                            CelluleEntete(entete, "Stock", true);
                            CelluleEntete(entete, "Seuil", true);
                            CelluleEntete(entete, "État");
                            CelluleEntete(entete, "Prix achat", true);
                            CelluleEntete(entete, "Prix vente", true);
                            CelluleEntete(entete, "Valeur", true);
                        });

                        var index = 0;

                        foreach (var ligne in lignes)
                        {
                            var fond = index++ % 2 == 0 ? Colors.White : StyleDocuments.CouleurFondAlterne;

                            Cellule(tableau, ligne.ProduitNom, fond);
                            Cellule(tableau, ligne.Sku, fond);
                            Cellule(tableau, $"{ligne.Couleur} / {ligne.Taille}", fond);
                            Cellule(tableau, ligne.QuantiteDisponible.ToString(), fond, true);
                            Cellule(tableau, ligne.SeuilMinimum.ToString(), fond, true);
                            Cellule(tableau, ligne.LibelleEtat, fond);
                            Cellule(tableau, FormatageMontant.FormaterSansDevise(ligne.PrixAchat), fond, true);
                            Cellule(tableau, FormatageMontant.FormaterSansDevise(ligne.PrixVente), fond, true);
                            Cellule(tableau, FormatageMontant.FormaterSansDevise(ligne.ValeurStock), fond, true);
                        }
                    });
                });

                page.Footer().Element(pied => PiedDePage(pied, magasin));
            });
        }).GeneratePdf();
    }

    // ==================================================================
    // Éléments de mise en page réutilisables
    // ==================================================================

    private static void ConfigurerPageA4(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(15, Unit.Millimetre);
        page.DefaultTextStyle(t => t
            .FontSize(StyleDocuments.TaillePolice)
            .FontColor(StyleDocuments.CouleurTexte));
    }

    private static void EnteteDocument(
        IContainer conteneur,
        ParametresMagasin magasin,
        string typeDocument,
        string numero,
        DateTime date)
    {
        conteneur.Column(colonne =>
        {
            colonne.Item().Row(ligne =>
            {
                ligne.RelativeItem().Column(gauche =>
                {
                    gauche.Item().Text(magasin.NomMagasin)
                        .Bold().FontSize(StyleDocuments.TaillePoliceTitre)
                        .FontColor(StyleDocuments.CouleurPrincipale);

                    if (!string.IsNullOrWhiteSpace(magasin.Adresse))
                    {
                        gauche.Item().Text(magasin.Adresse).FontSize(9);
                    }

                    if (!string.IsNullOrWhiteSpace(magasin.Telephone))
                    {
                        gauche.Item().Text($"Tél. : {magasin.Telephone}").FontSize(9);
                    }

                    if (!string.IsNullOrWhiteSpace(magasin.Email))
                    {
                        gauche.Item().Text(magasin.Email).FontSize(9);
                    }
                });

                ligne.ConstantItem(210).AlignRight().Column(droite =>
                {
                    droite.Item().AlignRight().Text(typeDocument)
                        .Bold().FontSize(16).FontColor(StyleDocuments.CouleurSecondaire);

                    droite.Item().AlignRight().Text($"N° {numero}").Bold().FontSize(11);

                    droite.Item().AlignRight()
                        .Text($"Date : {FormatageMontant.FormaterDateHeure(date)}").FontSize(9);
                });
            });

            colonne.Item().PaddingTop(6).LineHorizontal(1.5f)
                .LineColor(StyleDocuments.CouleurPrincipale);
        });
    }

    private static void EnteteRapport(
        IContainer conteneur,
        ParametresMagasin magasin,
        string titre,
        string sousTitre)
    {
        conteneur.Column(colonne =>
        {
            colonne.Item().Row(ligne =>
            {
                ligne.RelativeItem().Column(gauche =>
                {
                    gauche.Item().Text(magasin.NomMagasin)
                        .Bold().FontSize(StyleDocuments.TaillePoliceTitre)
                        .FontColor(StyleDocuments.CouleurPrincipale);

                    gauche.Item().Text(titre).Bold().FontSize(13)
                        .FontColor(StyleDocuments.CouleurSecondaire);

                    gauche.Item().Text(sousTitre).FontSize(10)
                        .FontColor(StyleDocuments.CouleurTexteAttenue);
                });

                ligne.ConstantItem(200).AlignRight()
                    .Text($"Édité le {FormatageMontant.FormaterDateHeure(DateTime.UtcNow)}")
                    .FontSize(8.5f).FontColor(StyleDocuments.CouleurTexteAttenue);
            });

            colonne.Item().PaddingTop(6).LineHorizontal(1.5f)
                .LineColor(StyleDocuments.CouleurPrincipale);
        });
    }

    private static void PiedDePage(IContainer conteneur, ParametresMagasin magasin)
    {
        conteneur.Column(colonne =>
        {
            colonne.Item().PaddingBottom(4).LineHorizontal(0.6f)
                .LineColor(StyleDocuments.CouleurBordure);

            colonne.Item().Row(ligne =>
            {
                var mentions = new List<string>();

                if (!string.IsNullOrWhiteSpace(magasin.RegistreCommerce))
                {
                    mentions.Add($"RC : {magasin.RegistreCommerce}");
                }

                if (!string.IsNullOrWhiteSpace(magasin.NumeroIdentificationFiscale))
                {
                    mentions.Add($"NIF : {magasin.NumeroIdentificationFiscale}");
                }

                if (!string.IsNullOrWhiteSpace(magasin.ArticleImposition))
                {
                    mentions.Add($"AI : {magasin.ArticleImposition}");
                }

                ligne.RelativeItem().Text(string.Join("  •  ", mentions))
                    .FontSize(7.5f).FontColor(StyleDocuments.CouleurTexteAttenue);

                ligne.ConstantItem(120).AlignRight().Text(texte =>
                {
                    texte.DefaultTextStyle(t => t.FontSize(7.5f)
                        .FontColor(StyleDocuments.CouleurTexteAttenue));
                    texte.Span("Page ");
                    texte.CurrentPageNumber();
                    texte.Span(" sur ");
                    texte.TotalPages();
                });
            });
        });
    }

    private static void TitreSection(IContainer conteneur, string titre) =>
        conteneur.PaddingTop(4).BorderLeft(3).BorderColor(StyleDocuments.CouleurSecondaire)
            .PaddingLeft(6)
            .Text(titre).Bold().FontSize(StyleDocuments.TaillePoliceSousTitre)
            .FontColor(StyleDocuments.CouleurPrincipale);

    private static void Indicateur(IContainer conteneur, string libelle, string valeur) =>
        conteneur.Background(StyleDocuments.CouleurFondEntete)
            .Border(0.6f).BorderColor(StyleDocuments.CouleurBordure)
            .Padding(8)
            .Column(colonne =>
            {
                colonne.Item().Text(libelle).FontSize(8)
                    .FontColor(StyleDocuments.CouleurTexteAttenue);

                colonne.Item().PaddingTop(2).Text(valeur).Bold().FontSize(12)
                    .FontColor(StyleDocuments.CouleurPrincipale);
            });

    private static void CelluleEntete(TableCellDescriptor entete, string texte, bool alignerADroite = false)
    {
        var cellule = entete.Cell()
            .Background(StyleDocuments.CouleurPrincipale)
            .PaddingVertical(5)
            .PaddingHorizontal(4);

        var conteneur = alignerADroite ? cellule.AlignRight() : cellule;

        conteneur.Text(texte).Bold().FontColor(Colors.White).FontSize(8.5f);
    }

    private static void Cellule(
        TableDescriptor tableau,
        string texte,
        Color fond,
        bool alignerADroite = false)
    {
        var cellule = tableau.Cell()
            .Background(fond)
            .BorderBottom(0.4f).BorderColor(StyleDocuments.CouleurBordure)
            .PaddingVertical(4)
            .PaddingHorizontal(4);

        var conteneur = alignerADroite ? cellule.AlignRight() : cellule;

        conteneur.Text(texte).FontSize(8.5f);
    }

    private static void LigneTotal(ColumnDescriptor colonne, string libelle, decimal montant, string devise) =>
        colonne.Item().Row(ligne =>
        {
            ligne.RelativeItem().Text(libelle);
            ligne.ConstantItem(70).AlignRight().Text(FormatageMontant.Formater(montant, devise));
        });

    private static void LigneTotalFacture(
        ColumnDescriptor colonne,
        string libelle,
        decimal montant,
        string devise) =>
        colonne.Item().Row(ligne =>
        {
            ligne.RelativeItem().Text(libelle).FontSize(9.5f);
            ligne.ConstantItem(110).AlignRight()
                .Text(FormatageMontant.Formater(montant, devise)).FontSize(9.5f);
        });
}
