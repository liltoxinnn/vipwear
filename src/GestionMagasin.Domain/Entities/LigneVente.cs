using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Article vendu sur un ticket. Le prix unitaire est figé au moment de la
/// vente : une évolution ultérieure du tarif ne doit jamais modifier
/// rétroactivement un ticket déjà encaissé.
/// </summary>
public class LigneVente : EntiteBase
{
    public int VenteId { get; set; }

    public Vente Vente { get; set; } = null!;

    public int VarianteProduitId { get; set; }

    public VarianteProduit VarianteProduit { get; set; } = null!;

    public int Quantite { get; set; }

    /// <summary>Prix de vente appliqué, conservé tel quel pour l'historique.</summary>
    public decimal PrixUnitaire { get; set; }

    /// <summary>Remise accordée sur cette ligne, en DZD.</summary>
    public decimal Remise { get; set; }

    public decimal Total { get; set; }

    /// <summary>
    /// Prix d'achat au moment de la vente, figé pour permettre un calcul de
    /// marge fidèle même si le prix d'achat évolue plus tard.
    /// </summary>
    public decimal PrixAchatUnitaire { get; set; }

    /// <summary>Quantité déjà reprise par un retour client.</summary>
    public int QuantiteRetournee { get; set; }

    /// <summary>Quantité de cette ligne encore susceptible d'être retournée.</summary>
    public int QuantiteRetournable => Quantite - QuantiteRetournee;
}
