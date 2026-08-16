using GestionMagasin.Domain.Common;
using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Article repris dans le cadre d'un retour. Selon l'état constaté, l'article
/// réintègre ou non le stock vendable : un article endommagé est tracé comme
/// tel mais n'augmente pas la quantité disponible.
/// </summary>
public class LigneRetour : EntiteBase
{
    public int RetourId { get; set; }

    public Retour Retour { get; set; } = null!;

    /// <summary>Ligne de la vente d'origine, qui plafonne la quantité reprenable.</summary>
    public int LigneVenteId { get; set; }

    public LigneVente LigneVente { get; set; } = null!;

    public int VarianteProduitId { get; set; }

    public VarianteProduit VarianteProduit { get; set; } = null!;

    public int Quantite { get; set; }

    public EtatArticleRetour EtatArticle { get; set; }

    /// <summary>
    /// Vrai lorsque l'article a effectivement augmenté le stock disponible.
    /// Toujours faux pour un article endommagé.
    /// </summary>
    public bool RemisEnStock { get; set; }

    /// <summary>Montant remboursé pour cette ligne, en DZD.</summary>
    public decimal MontantRembourse { get; set; }
}
