namespace GestionMagasin.Application.DTOs;

/// <summary>Bornes d'une période d'analyse, exprimées en heure universelle.</summary>
public record Periode
{
    public required DateTime DebutUtc { get; init; }

    public required DateTime FinUtc { get; init; }

    /// <summary>Intitulé affiché dans l'en-tête des rapports.</summary>
    public string Libelle { get; init; } = string.Empty;
}

/// <summary>Indicateurs clés du tableau de bord et du rapport de synthèse.</summary>
public record SyntheseActiviteDto
{
    /// <summary>Somme encaissée, hors ventes annulées et déduction faite des retours.</summary>
    public decimal ChiffreAffaires { get; init; }

    /// <summary>Chiffre d'affaires avant déduction des retours.</summary>
    public decimal ChiffreAffairesBrut { get; init; }

    public decimal MontantRetourne { get; init; }

    /// <summary>Marge estimée : prix de vente moins prix d'achat figé à la vente.</summary>
    public decimal BeneficeEstime { get; init; }

    public int NombreVentes { get; init; }

    public int ArticlesVendus { get; init; }

    public int NombreRetours { get; init; }

    public decimal TotalAchats { get; init; }

    /// <summary>Panier moyen sur la période.</summary>
    public decimal TicketMoyen => NombreVentes > 0
        ? Math.Round(ChiffreAffaires / NombreVentes, 2)
        : 0m;

    /// <summary>Taux de marge en pourcentage du chiffre d'affaires.</summary>
    public decimal TauxMarge => ChiffreAffaires > 0
        ? Math.Round(BeneficeEstime / ChiffreAffaires * 100m, 2)
        : 0m;
}

/// <summary>Point d'une courbe ou barre d'un histogramme.</summary>
public record PointStatistique
{
    public string Libelle { get; init; } = string.Empty;

    public DateTime? Date { get; init; }

    public decimal Valeur { get; init; }

    /// <summary>Seconde grandeur, par exemple le nombre de ventes derrière un montant.</summary>
    public decimal ValeurSecondaire { get; init; }
}

/// <summary>Ligne du classement des articles.</summary>
public record ClassementArticleDto
{
    public int VarianteProduitId { get; init; }

    public int ProduitId { get; init; }

    public string Designation { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string? Marque { get; init; }

    public int QuantiteVendue { get; init; }

    public decimal ChiffreAffaires { get; init; }

    public decimal Benefice { get; init; }

    public int StockActuel { get; init; }
}

/// <summary>Ligne du rapport « Ventes par employé ».</summary>
public record VentesParEmployeDto
{
    public int UtilisateurId { get; init; }

    public string Utilisateur { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public int NombreVentes { get; init; }

    public int ArticlesVendus { get; init; }

    public decimal ChiffreAffaires { get; init; }

    public decimal TicketMoyen => NombreVentes > 0
        ? Math.Round(ChiffreAffaires / NombreVentes, 2)
        : 0m;
}

/// <summary>Ligne du rapport « Ventes par mode de paiement ».</summary>
public record VentesParModePaiementDto
{
    public string ModePaiement { get; init; } = string.Empty;

    public int NombreReglements { get; init; }

    public decimal Montant { get; init; }

    /// <summary>Part du mode de règlement dans le total encaissé.</summary>
    public decimal Pourcentage { get; init; }
}

/// <summary>Synthèse des achats fournisseurs sur une période.</summary>
public record SyntheseAchatsDto
{
    public int NombreCommandes { get; init; }

    public int ArticlesCommandes { get; init; }

    public int ArticlesRecus { get; init; }

    public decimal TotalCommandes { get; init; }

    public decimal TotalPaye { get; init; }

    public decimal TotalRestantDu { get; init; }
}

/// <summary>Synthèse des retours sur une période.</summary>
public record SyntheseRetoursDto
{
    public int NombreRetours { get; init; }

    public int ArticlesRepris { get; init; }

    public int ArticlesRemisEnStock { get; init; }

    public int ArticlesEndommages { get; init; }

    public decimal TotalRembourse { get; init; }

    /// <summary>Part des ventes ayant donné lieu à un retour, en pourcentage.</summary>
    public decimal TauxRetour { get; init; }
}

/// <summary>Synthèse de la valeur du stock.</summary>
public record SyntheseStockDto
{
    public int NombreReferences { get; init; }

    public int NombreDeclinaisons { get; init; }

    public int QuantiteTotale { get; init; }

    public decimal ValeurAuPrixAchat { get; init; }

    public decimal ValeurAuPrixVente { get; init; }

    public int NombreStockFaible { get; init; }

    public int NombreRuptures { get; init; }

    /// <summary>Marge potentielle si tout le stock était vendu au prix affiché.</summary>
    public decimal MargePotentielle => ValeurAuPrixVente - ValeurAuPrixAchat;
}

/// <summary>Ensemble des données affichées sur le tableau de bord.</summary>
public record TableauBordDto
{
    public SyntheseActiviteDto Jour { get; init; } = new();

    public SyntheseActiviteDto Mois { get; init; } = new();

    public SyntheseStockDto Stock { get; init; } = new();

    /// <summary>Chiffre d'affaires des trente derniers jours.</summary>
    public IReadOnlyList<PointStatistique> EvolutionVentes { get; init; } = Array.Empty<PointStatistique>();

    /// <summary>Chiffre d'affaires des douze derniers mois.</summary>
    public IReadOnlyList<PointStatistique> EvolutionMensuelle { get; init; } = Array.Empty<PointStatistique>();

    public IReadOnlyList<ClassementArticleDto> MeilleuresVentes { get; init; } = Array.Empty<ClassementArticleDto>();

    public IReadOnlyList<ResumeVenteDto> DernieresVentes { get; init; } = Array.Empty<ResumeVenteDto>();

    public IReadOnlyList<LigneStockDto> StockFaible { get; init; } = Array.Empty<LigneStockDto>();

    public IReadOnlyList<LigneStockDto> Ruptures { get; init; } = Array.Empty<LigneStockDto>();
}
