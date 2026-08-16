using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Quantité actuellement disponible pour une déclinaison de produit.
/// Une seule ligne existe par variante. Cette quantité n'est modifiée que par
/// le service de stock centralisé, jamais directement depuis l'interface.
/// </summary>
public class Inventaire : EntiteBase
{
    public int VarianteProduitId { get; set; }

    public VarianteProduit VarianteProduit { get; set; } = null!;

    /// <summary>
    /// Quantité en rayon. Une contrainte en base interdit toute valeur négative.
    /// </summary>
    public int QuantiteDisponible { get; set; }

    public DateTime DateModification { get; set; }

    /// <summary>
    /// Jeton de concurrence PostgreSQL (colonne système xmin). Il empêche deux
    /// postes de caisse d'écraser mutuellement la même quantité.
    /// </summary>
    public uint VersionEnregistrement { get; set; }
}
