using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Domain.Securite;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Application.Services;

/// <inheritdoc cref="IServiceRapports"/>
public class ServiceRapports : IServiceRapports
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IServiceStock _stock;
    private readonly IServiceVentes _ventes;
    private readonly ISessionUtilisateur _session;
    private readonly IFournisseurHorodatage _horodatage;

    public ServiceRapports(
        IUniteDeTravail uniteDeTravail,
        IServiceStock stock,
        IServiceVentes ventes,
        ISessionUtilisateur session,
        IFournisseurHorodatage horodatage)
    {
        _uniteDeTravail = uniteDeTravail;
        _stock = stock;
        _ventes = ventes;
        _session = session;
        _horodatage = horodatage;
    }

    // ==================================================================
    // Synthèse d'activité
    // ==================================================================

    public async Task<SyntheseActiviteDto> ObtenirSyntheseAsync(
        Periode periode,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        var ventes = VentesRetenues(periode);

        var chiffreBrut = await ventes.SumAsync(v => (decimal?)v.Total, jeton).ConfigureAwait(false) ?? 0m;
        var nombreVentes = await ventes.CountAsync(jeton).ConfigureAwait(false);

        var lignes = LignesVenteRetenues(periode);

        var articlesSortis = await lignes.SumAsync(l => (int?)l.Quantite, jeton).ConfigureAwait(false) ?? 0;

        // Les articles rendus sont déduits, comme l'est le chiffre d'affaires
        // juste à côté. Un article vendu puis repris ne doit pas rester compté
        // parmi les articles vendus du jour.
        var articlesRendus = await LignesRetourRetenues(periode)
            .SumAsync(l => (int?)l.Quantite, jeton)
            .ConfigureAwait(false) ?? 0;

        // Coût des marchandises vendues, calculé sur le prix d'achat figé au
        // moment de la vente.
        var coutMarchandises = await lignes
            .SumAsync(l => (decimal?)(l.PrixAchatUnitaire * l.Quantite), jeton)
            .ConfigureAwait(false) ?? 0m;

        var retours = RetoursRetenus(periode);

        var nombreRetours = await retours.CountAsync(jeton).ConfigureAwait(false);
        var montantRetourne = await retours
            .SumAsync(r => (decimal?)r.TotalRembourse, jeton)
            .ConfigureAwait(false) ?? 0m;

        // Un article revendable qui revient en stock annule son coût d'achat.
        // Un article endommagé reste une perte sèche : son coût est conservé.
        var coutMarchandisesReprises = await _uniteDeTravail.Depot<LigneRetour>().Requete()
            .Where(l => l.Retour.DateRetour >= periode.DebutUtc
                        && l.Retour.DateRetour < periode.FinUtc
                        && l.Retour.Statut == StatutRetour.Valide
                        && l.RemisEnStock)
            .SumAsync(l => (decimal?)(l.LigneVente.PrixAchatUnitaire * l.Quantite), jeton)
            .ConfigureAwait(false) ?? 0m;

        var totalAchats = await _uniteDeTravail.Depot<Achat>().Requete()
            .Where(a => a.DateAchat >= periode.DebutUtc
                        && a.DateAchat < periode.FinUtc
                        && a.Statut != StatutAchat.Annule)
            .SumAsync(a => (decimal?)a.Total, jeton)
            .ConfigureAwait(false) ?? 0m;

        return new SyntheseActiviteDto
        {
            ChiffreAffairesBrut = chiffreBrut,
            MontantRetourne = montantRetourne,
            ChiffreAffaires = chiffreBrut - montantRetourne,
            BeneficeEstime = chiffreBrut - coutMarchandises - montantRetourne + coutMarchandisesReprises,
            NombreVentes = nombreVentes,
            ArticlesVendus = articlesSortis - articlesRendus,
            NombreRetours = nombreRetours,
            TotalAchats = totalAchats
        };
    }

    // ==================================================================
    // Évolutions
    // ==================================================================

    public async Task<IReadOnlyList<PointStatistique>> ObtenirVentesParJourAsync(
        Periode periode,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        // Les dates sont conservées en heure universelle mais regroupées en
        // heure locale : une vente de 00 h 30 doit compter pour sa journée
        // réelle au magasin, pas pour la veille.
        var ventes = await VentesRetenues(periode)
            .Select(v => new { v.DateVente, v.Total })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var parJour = ventes
            .GroupBy(v => v.DateVente.ToLocalTime().Date)
            .ToDictionary(g => g.Key, g => new { Montant = g.Sum(v => v.Total), Nombre = g.Count() });

        var debutLocal = periode.DebutUtc.ToLocalTime().Date;
        var finLocale = periode.FinUtc.ToLocalTime().Date;

        var points = new List<PointStatistique>();

        // Les journées sans vente apparaissent à zéro : la courbe reste
        // continue et lisible.
        for (var jour = debutLocal; jour < finLocale; jour = jour.AddDays(1))
        {
            parJour.TryGetValue(jour, out var valeur);

            points.Add(new PointStatistique
            {
                Libelle = jour.ToString("dd/MM"),
                Date = jour,
                Valeur = valeur?.Montant ?? 0m,
                ValeurSecondaire = valeur?.Nombre ?? 0
            });
        }

        return points;
    }

    public async Task<IReadOnlyList<PointStatistique>> ObtenirVentesParMoisAsync(
        Periode periode,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        var ventes = await VentesRetenues(periode)
            .Select(v => new { v.DateVente, v.Total })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var parMois = ventes
            .GroupBy(v =>
            {
                var locale = v.DateVente.ToLocalTime();
                return new DateTime(locale.Year, locale.Month, 1);
            })
            .ToDictionary(g => g.Key, g => new { Montant = g.Sum(v => v.Total), Nombre = g.Count() });

        var debutLocal = periode.DebutUtc.ToLocalTime();
        var finLocale = periode.FinUtc.ToLocalTime();

        var premierMois = new DateTime(debutLocal.Year, debutLocal.Month, 1);
        var dernierMois = new DateTime(finLocale.Year, finLocale.Month, 1);

        var points = new List<PointStatistique>();

        for (var mois = premierMois; mois < dernierMois; mois = mois.AddMonths(1))
        {
            parMois.TryGetValue(mois, out var valeur);

            points.Add(new PointStatistique
            {
                Libelle = mois.ToString("MMM yyyy"),
                Date = mois,
                Valeur = valeur?.Montant ?? 0m,
                ValeurSecondaire = valeur?.Nombre ?? 0
            });
        }

        return points;
    }

    // ==================================================================
    // Classements d'articles
    // ==================================================================

    public async Task<IReadOnlyList<ClassementArticleDto>> ObtenirMeilleuresVentesAsync(
        Periode periode,
        int limite = 20,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        var classement = await ClassementNetAsync(periode, jeton).ConfigureAwait(false);

        // Un article rendu n'est plus une vente. Sans ce filtre, un article
        // vendu puis repris le même jour resterait en tête des meilleures
        // ventes alors qu'il n'a rien rapporté — ce que le chiffre d'affaires
        // affiché juste au-dessus dément.
        var retenus = classement
            .Where(c => c.QuantiteVendue > 0)
            .OrderByDescending(c => c.QuantiteVendue)
            .ThenByDescending(c => c.ChiffreAffaires)
            .Take(limite)
            .ToList();

        return await DecrireAsync(retenus, jeton).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassementArticleDto>> ObtenirMoinsBonnesVentesAsync(
        Periode periode,
        int limite = 20,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        // Le classement part du catalogue, et non des ventes : les
        // déclinaisons jamais vendues, qui immobilisent du stock sans rien
        // rapporter, doivent apparaître en tête de cette liste.
        var ventesParVariante = (await ClassementNetAsync(periode, jeton).ConfigureAwait(false))
            .ToDictionary(c => c.VarianteProduitId);

        var variantes = await _uniteDeTravail.Variantes.Requete()
            .Where(v => v.Actif && v.Produit.Actif)
            .Select(v => new
            {
                v.Id,
                v.ProduitId,
                ProduitNom = v.Produit.Nom,
                Couleur = v.Couleur.Nom,
                Taille = v.Taille.Nom,
                v.SKU,
                Marque = v.Produit.Marque != null ? v.Produit.Marque.Nom : null,
                Stock = v.Inventaire != null ? v.Inventaire.QuantiteDisponible : 0
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return variantes
            .Select(v =>
            {
                ventesParVariante.TryGetValue(v.Id, out var vente);

                return new ClassementArticleDto
                {
                    VarianteProduitId = v.Id,
                    ProduitId = v.ProduitId,
                    Designation = $"{v.ProduitNom} — {v.Couleur} / {v.Taille}",
                    Sku = v.SKU,
                    Marque = v.Marque,
                    QuantiteVendue = vente?.QuantiteVendue ?? 0,
                    ChiffreAffaires = vente?.ChiffreAffaires ?? 0m,
                    Benefice = vente?.Benefice ?? 0m,
                    StockActuel = v.Stock
                };
            })
            .OrderBy(c => c.QuantiteVendue)
            .ThenByDescending(c => c.StockActuel)
            .Take(limite)
            .ToList();
    }

    /// <summary>
    /// Ventes de la période, déduction faite des retours.
    ///
    /// Les retours sont rattachés à la période où ils ont été enregistrés,
    /// comme le sont le chiffre d'affaires et le bénéfice de la synthèse :
    /// les classements et les cartes du tableau de bord reposent ainsi sur
    /// les mêmes chiffres, ce que l'utilisateur vérifie d'un coup d'œil.
    ///
    /// Un article rendu peut donc afficher une quantité négative — vendu le
    /// mois passé, repris ce mois-ci. C'est voulu : l'appelant décide s'il
    /// écarte ces lignes ou les montre.
    /// </summary>
    private async Task<List<ClassementArticleDto>> ClassementNetAsync(
        Periode periode,
        CancellationToken jeton)
    {
        var ventes = await LignesVenteRetenues(periode)
            .GroupBy(l => l.VarianteProduitId)
            .Select(g => new
            {
                VarianteProduitId = g.Key,
                Quantite = g.Sum(l => l.Quantite),
                ChiffreAffaires = g.Sum(l => l.Total),
                Benefice = g.Sum(l => l.Total - (l.PrixAchatUnitaire * l.Quantite))
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var retours = await LignesRetourRetenues(periode)
            .GroupBy(l => l.VarianteProduitId)
            .Select(g => new
            {
                VarianteProduitId = g.Key,
                Quantite = g.Sum(l => l.Quantite),
                Rembourse = g.Sum(l => l.MontantRembourse),
                // Un article revendable qui revient en stock annule son coût
                // d'achat ; un article endommagé reste une perte sèche.
                CoutRepris = g.Sum(l => l.RemisEnStock
                    ? l.LigneVente.PrixAchatUnitaire * l.Quantite
                    : 0m)
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var parVariante = ventes.ToDictionary(
            v => v.VarianteProduitId,
            v => new ClassementArticleDto
            {
                VarianteProduitId = v.VarianteProduitId,
                QuantiteVendue = v.Quantite,
                ChiffreAffaires = v.ChiffreAffaires,
                Benefice = v.Benefice
            });

        foreach (var retour in retours)
        {
            parVariante.TryGetValue(retour.VarianteProduitId, out var vendu);

            parVariante[retour.VarianteProduitId] = new ClassementArticleDto
            {
                VarianteProduitId = retour.VarianteProduitId,
                QuantiteVendue = (vendu?.QuantiteVendue ?? 0) - retour.Quantite,
                ChiffreAffaires = (vendu?.ChiffreAffaires ?? 0m) - retour.Rembourse,
                Benefice = (vendu?.Benefice ?? 0m) - retour.Rembourse + retour.CoutRepris
            };
        }

        return parVariante.Values.ToList();
    }

    /// <summary>
    /// Complète un classement avec la désignation, la marque et le stock des
    /// déclinaisons concernées. La description est demandée après le
    /// classement, et pour les seules lignes retenues.
    /// </summary>
    private async Task<IReadOnlyList<ClassementArticleDto>> DecrireAsync(
        IReadOnlyList<ClassementArticleDto> classement,
        CancellationToken jeton)
    {
        if (classement.Count == 0)
        {
            return Array.Empty<ClassementArticleDto>();
        }

        var identifiants = classement.Select(c => c.VarianteProduitId).ToList();

        var descriptions = await _uniteDeTravail.Variantes.Requete()
            .Where(v => identifiants.Contains(v.Id))
            .Select(v => new
            {
                v.Id,
                v.ProduitId,
                ProduitNom = v.Produit.Nom,
                Couleur = v.Couleur.Nom,
                Taille = v.Taille.Nom,
                v.SKU,
                Marque = v.Produit.Marque != null ? v.Produit.Marque.Nom : null,
                Stock = v.Inventaire != null ? v.Inventaire.QuantiteDisponible : 0
            })
            .ToDictionaryAsync(v => v.Id, jeton)
            .ConfigureAwait(false);

        return classement
            .Where(c => descriptions.ContainsKey(c.VarianteProduitId))
            .Select(c =>
            {
                var description = descriptions[c.VarianteProduitId];

                return c with
                {
                    ProduitId = description.ProduitId,
                    Designation = $"{description.ProduitNom} — {description.Couleur} / {description.Taille}",
                    Sku = description.SKU,
                    Marque = description.Marque,
                    StockActuel = description.Stock
                };
            })
            .ToList();
    }

    // ==================================================================
    // Répartitions
    // ==================================================================

    public async Task<IReadOnlyList<VentesParEmployeDto>> ObtenirVentesParEmployeAsync(
        Periode periode,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        var resultats = await VentesRetenues(periode)
            .GroupBy(v => new { v.UtilisateurId, v.Utilisateur.Prenom, v.Utilisateur.Nom, Role = v.Utilisateur.Role.Nom })
            .Select(g => new
            {
                g.Key.UtilisateurId,
                g.Key.Prenom,
                g.Key.Nom,
                g.Key.Role,
                NombreVentes = g.Count(),
                ChiffreAffaires = g.Sum(v => v.Total)
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var articlesParUtilisateur = await LignesVenteRetenues(periode)
            .GroupBy(l => l.Vente.UtilisateurId)
            .Select(g => new { UtilisateurId = g.Key, Articles = g.Sum(l => l.Quantite) })
            .ToDictionaryAsync(g => g.UtilisateurId, g => g.Articles, jeton)
            .ConfigureAwait(false);

        return resultats
            .Select(r => new VentesParEmployeDto
            {
                UtilisateurId = r.UtilisateurId,
                Utilisateur = $"{r.Prenom} {r.Nom}".Trim(),
                Role = r.Role,
                NombreVentes = r.NombreVentes,
                ArticlesVendus = articlesParUtilisateur.GetValueOrDefault(r.UtilisateurId),
                ChiffreAffaires = r.ChiffreAffaires
            })
            .OrderByDescending(r => r.ChiffreAffaires)
            .ToList();
    }

    public async Task<IReadOnlyList<VentesParModePaiementDto>> ObtenirVentesParModePaiementAsync(
        Periode periode,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        var resultats = await _uniteDeTravail.Depot<Paiement>().Requete()
            .Where(p => p.DatePaiement >= periode.DebutUtc
                        && p.DatePaiement < periode.FinUtc
                        && p.Vente.Statut != StatutVente.Annulee)
            .GroupBy(p => p.ModePaiement)
            .Select(g => new
            {
                ModePaiement = g.Key,
                NombreReglements = g.Count(),
                Montant = g.Sum(p => p.Montant)
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var total = resultats.Sum(r => r.Montant);

        return resultats
            .Select(r => new VentesParModePaiementDto
            {
                ModePaiement = r.ModePaiement.Libelle(),
                NombreReglements = r.NombreReglements,
                Montant = r.Montant,
                Pourcentage = total > 0 ? Math.Round(r.Montant / total * 100m, 2) : 0m
            })
            .OrderByDescending(r => r.Montant)
            .ToList();
    }

    // ==================================================================
    // Achats, retours et stock
    // ==================================================================

    public async Task<SyntheseAchatsDto> ObtenirSyntheseAchatsAsync(
        Periode periode,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        var achats = _uniteDeTravail.Depot<Achat>().Requete()
            .Where(a => a.DateAchat >= periode.DebutUtc
                        && a.DateAchat < periode.FinUtc
                        && a.Statut != StatutAchat.Annule);

        var lignes = _uniteDeTravail.Depot<LigneAchat>().Requete()
            .Where(l => l.Achat.DateAchat >= periode.DebutUtc
                        && l.Achat.DateAchat < periode.FinUtc
                        && l.Achat.Statut != StatutAchat.Annule);

        return new SyntheseAchatsDto
        {
            NombreCommandes = await achats.CountAsync(jeton).ConfigureAwait(false),
            ArticlesCommandes = await lignes.SumAsync(l => (int?)l.Quantite, jeton).ConfigureAwait(false) ?? 0,
            ArticlesRecus = await lignes.SumAsync(l => (int?)l.QuantiteRecue, jeton).ConfigureAwait(false) ?? 0,
            TotalCommandes = await achats.SumAsync(a => (decimal?)a.Total, jeton).ConfigureAwait(false) ?? 0m,
            TotalPaye = await achats.SumAsync(a => (decimal?)a.MontantPaye, jeton).ConfigureAwait(false) ?? 0m,
            TotalRestantDu = await achats.SumAsync(a => (decimal?)a.MontantRestant, jeton).ConfigureAwait(false) ?? 0m
        };
    }

    public async Task<SyntheseRetoursDto> ObtenirSyntheseRetoursAsync(
        Periode periode,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        var retours = RetoursRetenus(periode);

        var lignes = _uniteDeTravail.Depot<LigneRetour>().Requete()
            .Where(l => l.Retour.DateRetour >= periode.DebutUtc
                        && l.Retour.DateRetour < periode.FinUtc
                        && l.Retour.Statut == StatutRetour.Valide);

        var articlesRepris = await lignes.SumAsync(l => (int?)l.Quantite, jeton).ConfigureAwait(false) ?? 0;

        var articlesRemisEnStock = await lignes
            .Where(l => l.RemisEnStock)
            .SumAsync(l => (int?)l.Quantite, jeton)
            .ConfigureAwait(false) ?? 0;

        var articlesVendus = await LignesVenteRetenues(periode)
            .SumAsync(l => (int?)l.Quantite, jeton)
            .ConfigureAwait(false) ?? 0;

        return new SyntheseRetoursDto
        {
            NombreRetours = await retours.CountAsync(jeton).ConfigureAwait(false),
            ArticlesRepris = articlesRepris,
            ArticlesRemisEnStock = articlesRemisEnStock,
            ArticlesEndommages = articlesRepris - articlesRemisEnStock,
            TotalRembourse = await retours.SumAsync(r => (decimal?)r.TotalRembourse, jeton).ConfigureAwait(false) ?? 0m,
            TauxRetour = articlesVendus > 0
                ? Math.Round((decimal)articlesRepris / articlesVendus * 100m, 2)
                : 0m
        };
    }

    public async Task<SyntheseStockDto> ObtenirSyntheseStockAsync(CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        var variantes = _uniteDeTravail.Variantes.Requete()
            .Where(v => v.Actif && v.Produit.Actif);

        var quantiteTotale = await variantes
            .SumAsync(v => v.Inventaire != null ? (int?)v.Inventaire.QuantiteDisponible : 0, jeton)
            .ConfigureAwait(false) ?? 0;

        var valeurAchat = await variantes
            .Where(v => v.Inventaire != null)
            .SumAsync(v => (decimal?)(v.Inventaire!.QuantiteDisponible * (v.PrixAchat ?? v.Produit.PrixAchat)), jeton)
            .ConfigureAwait(false) ?? 0m;

        var valeurVente = await variantes
            .Where(v => v.Inventaire != null)
            .SumAsync(v => (decimal?)(v.Inventaire!.QuantiteDisponible * (v.PrixVente ?? v.Produit.PrixVente)), jeton)
            .ConfigureAwait(false) ?? 0m;

        return new SyntheseStockDto
        {
            NombreReferences = await _uniteDeTravail.Depot<Produit>().Requete()
                .CountAsync(p => p.Actif, jeton).ConfigureAwait(false),
            NombreDeclinaisons = await variantes.CountAsync(jeton).ConfigureAwait(false),
            QuantiteTotale = quantiteTotale,
            ValeurAuPrixAchat = valeurAchat,
            ValeurAuPrixVente = valeurVente,
            NombreStockFaible = await variantes.CountAsync(
                v => v.Inventaire != null
                     && v.Inventaire.QuantiteDisponible > 0
                     && v.SeuilMinimum > 0
                     && v.Inventaire.QuantiteDisponible <= v.SeuilMinimum,
                jeton).ConfigureAwait(false),
            NombreRuptures = await variantes.CountAsync(
                v => v.Inventaire == null || v.Inventaire.QuantiteDisponible <= 0,
                jeton).ConfigureAwait(false)
        };
    }

    // ==================================================================
    // Tableau de bord
    // ==================================================================

    public async Task<TableauBordDto> ObtenirTableauBordAsync(CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirRapports);

        var maintenant = _horodatage.MaintenantLocal;

        var journee = CalculateurPeriode.Aujourdhui(maintenant);
        var mois = CalculateurPeriode.MoisEnCours(maintenant);
        var trenteJours = CalculateurPeriode.DerniersJours(maintenant, 30);
        var douzeMois = CalculateurPeriode.DerniersMois(maintenant, 12);

        return new TableauBordDto
        {
            Jour = await ObtenirSyntheseAsync(journee, jeton).ConfigureAwait(false),
            Mois = await ObtenirSyntheseAsync(mois, jeton).ConfigureAwait(false),
            Stock = await ObtenirSyntheseStockAsync(jeton).ConfigureAwait(false),
            EvolutionVentes = await ObtenirVentesParJourAsync(trenteJours, jeton).ConfigureAwait(false),
            EvolutionMensuelle = await ObtenirVentesParMoisAsync(douzeMois, jeton).ConfigureAwait(false),
            MeilleuresVentes = await ObtenirMeilleuresVentesAsync(mois, 8, jeton).ConfigureAwait(false),
            DernieresVentes = await _ventes.RechercherAsync(limite: 10, jeton: jeton).ConfigureAwait(false),
            StockFaible = await _stock.ListerStockFaibleAsync(10, jeton).ConfigureAwait(false),
            Ruptures = await _stock.ListerRupturesAsync(10, jeton).ConfigureAwait(false)
        };
    }

    // ==================================================================
    // Requêtes de base partagées
    // ==================================================================

    /// <summary>
    /// Ventes prises en compte dans les statistiques : celles de la période,
    /// hors ventes annulées qui ne correspondent à aucune recette réelle.
    /// </summary>
    private IQueryable<Vente> VentesRetenues(Periode periode) =>
        _uniteDeTravail.Depot<Vente>().Requete()
            .Where(v => v.DateVente >= periode.DebutUtc
                        && v.DateVente < periode.FinUtc
                        && v.Statut != StatutVente.Annulee);

    private IQueryable<LigneVente> LignesVenteRetenues(Periode periode) =>
        _uniteDeTravail.Depot<LigneVente>().Requete()
            .Where(l => l.Vente.DateVente >= periode.DebutUtc
                        && l.Vente.DateVente < periode.FinUtc
                        && l.Vente.Statut != StatutVente.Annulee);

    private IQueryable<LigneRetour> LignesRetourRetenues(Periode periode) =>
        _uniteDeTravail.Depot<LigneRetour>().Requete()
            .Where(l => l.Retour.DateRetour >= periode.DebutUtc
                        && l.Retour.DateRetour < periode.FinUtc
                        && l.Retour.Statut == StatutRetour.Valide);

    private IQueryable<Retour> RetoursRetenus(Periode periode) =>
        _uniteDeTravail.Depot<Retour>().Requete()
            .Where(r => r.DateRetour >= periode.DebutUtc
                        && r.DateRetour < periode.FinUtc
                        && r.Statut == StatutRetour.Valide);
}
