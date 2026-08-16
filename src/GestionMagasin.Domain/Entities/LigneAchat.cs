using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Ligne de détail d'une commande fournisseur, rattachée à une déclinaison
/// précise (couleur + taille). <see cref="QuantiteRecue"/> permet de gérer
/// les réceptions partielles sans perdre l'information de la commande initiale.
/// </summary>
public class LigneAchat : EntiteBase
{
    public int AchatId { get; set; }

    public Achat Achat { get; set; } = null!;

    public int VarianteProduitId { get; set; }

    public VarianteProduit VarianteProduit { get; set; } = null!;

    /// <summary>Quantité commandée.</summary>
    public int Quantite { get; set; }

    /// <summary>Quantité effectivement réceptionnée et intégrée au stock.</summary>
    public int QuantiteRecue { get; set; }

    public decimal PrixUnitaire { get; set; }

    public decimal Total { get; set; }

    /// <summary>Quantité commandée qui reste à recevoir.</summary>
    public int QuantiteRestante => Quantite - QuantiteRecue;
}
