namespace GestionMagasin.Application.DTOs;

/// <summary>Déclinaison telle qu'affichée dans le tableau des variantes d'un produit.</summary>
public record VarianteDto
{
    public int Id { get; init; }

    public int ProduitId { get; init; }

    /// <summary>Nom du produit. Sans lui, « Beige / M » ne désigne rien.</summary>
    public string ProduitNom { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public string? Marque { get; init; }

    /// <summary>Famille du produit, employée pour les onglets du rayon.</summary>
    public string Categorie { get; init; } = string.Empty;

    public int TailleId { get; init; }

    public string Taille { get; init; } = string.Empty;

    public int OrdreTaille { get; init; }

    public int CouleurId { get; init; }

    public string Couleur { get; init; } = string.Empty;

    public string? CodeCouleur { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string? CodeBarres { get; init; }

    /// <summary>Prix propre à la déclinaison ; vide lorsqu'elle suit le prix du produit.</summary>
    public decimal? PrixAchatSpecifique { get; init; }

    public decimal? PrixVenteSpecifique { get; init; }

    /// <summary>Prix réellement appliqué (déclinaison si défini, sinon produit).</summary>
    public decimal PrixAchat { get; init; }

    public decimal PrixVente { get; init; }

    public int SeuilMinimum { get; init; }

    public int QuantiteDisponible { get; init; }

    public bool Actif { get; init; }

    public bool EnRupture => QuantiteDisponible <= 0;

    public bool StockFaible => QuantiteDisponible > 0
                               && SeuilMinimum > 0
                               && QuantiteDisponible <= SeuilMinimum;

    public string LibelleEtat => !Actif ? "Inactive"
        : EnRupture ? "Rupture"
        : StockFaible ? "Stock faible"
        : "Disponible";

    /// <summary>Marge unitaire dégagée par cette déclinaison.</summary>
    public decimal Marge => PrixVente - PrixAchat;

    /// <summary>Libellé complet, tel qu'il doit apparaître sur un ticket.</summary>
    public string Designation => string.IsNullOrEmpty(ProduitNom)
        ? $"{Couleur} / {Taille}"
        : $"{ProduitNom} — {Couleur} / {Taille}";
}

/// <summary>Fiche produit complète, avec toutes ses déclinaisons.</summary>
public record ProduitDto
{
    public int Id { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string Nom { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int? MarqueId { get; init; }

    public string? Marque { get; init; }

    /// <summary>Famille de l'article : elle commande le système de tailles.</summary>
    public int CategorieId { get; init; }

    public string Categorie { get; init; } = string.Empty;

    public int SystemeTailleId { get; init; }

    public string SystemeTaille { get; init; } = string.Empty;

    public string? Collection { get; init; }

    public string? Saison { get; init; }

    public decimal PrixAchat { get; init; }

    public decimal PrixVente { get; init; }

    public string? Photo { get; init; }

    public bool Actif { get; init; }

    public DateTime DateCreation { get; init; }

    public DateTime? DateModification { get; init; }

    public IReadOnlyList<VarianteDto> Variantes { get; init; } = Array.Empty<VarianteDto>();

    /// <summary>Stock total du produit, toutes déclinaisons confondues.</summary>
    public int StockTotal => Variantes.Sum(v => v.QuantiteDisponible);

    public int NombreVariantes => Variantes.Count;

    public decimal Marge => PrixVente - PrixAchat;

    /// <summary>Taux de marge en pourcentage du prix de vente.</summary>
    public decimal TauxMarge => PrixVente > 0 ? Math.Round((PrixVente - PrixAchat) / PrixVente * 100m, 2) : 0m;
}

/// <summary>Ligne de la liste des produits.</summary>
public record ResumeProduitDto
{
    public int Id { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string Nom { get; init; } = string.Empty;

    public string? Marque { get; init; }

    public string Categorie { get; init; } = string.Empty;

    public string? Collection { get; init; }

    public string? Saison { get; init; }

    public decimal PrixAchat { get; init; }

    public decimal PrixVente { get; init; }

    public int NombreVariantes { get; init; }

    public int StockTotal { get; init; }

    public bool Actif { get; init; }

    public decimal Marge => PrixVente - PrixAchat;
}

/// <summary>Données de création ou de modification d'un produit.</summary>
public record DemandeProduit
{
    public string Reference { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string Nom { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int? MarqueId { get; init; }

    /// <summary>
    /// Famille de l'article. Obligatoire : sans elle, la génération des
    /// déclinaisons ne saurait pas quelles tailles proposer.
    /// </summary>
    public int CategorieId { get; init; }

    public string? Collection { get; init; }

    public string? Saison { get; init; }

    public decimal PrixAchat { get; init; }

    public decimal PrixVente { get; init; }

    public string? Photo { get; init; }
}

/// <summary>Données de création ou de modification d'une déclinaison.</summary>
public record DemandeVariante
{
    public int TailleId { get; init; }

    public int CouleurId { get; init; }

    /// <summary>Laisser vide pour que le logiciel compose un code article.</summary>
    public string? Sku { get; init; }

    public string? CodeBarres { get; init; }

    public decimal? PrixAchat { get; init; }

    public decimal? PrixVente { get; init; }

    public int SeuilMinimum { get; init; }

    /// <summary>Quantité initiale à mettre en stock lors de la création.</summary>
    public int QuantiteInitiale { get; init; }
}

/// <summary>Élément de référence simple (marque, taille, couleur).</summary>
public record ReferenceDto
{
    public int Id { get; init; }

    public string Nom { get; init; } = string.Empty;

    public string? Complement { get; init; }

    public int Ordre { get; init; }

    public bool Actif { get; init; }

    /// <summary>Nombre d'éléments rattachés, pour prévenir avant désactivation.</summary>
    public int NombreUtilisations { get; init; }
}

/// <summary>
/// Famille d'articles, avec le système de tailles qu'elle impose.
/// </summary>
public record CategorieDto
{
    public int Id { get; init; }

    public string Nom { get; init; } = string.Empty;

    public int SystemeTailleId { get; init; }

    public string SystemeTaille { get; init; } = string.Empty;

    public int Ordre { get; init; }

    public bool Actif { get; init; }

    /// <summary>Nombre d'articles rattachés, pour prévenir avant désactivation.</summary>
    public int NombreProduits { get; init; }
}
