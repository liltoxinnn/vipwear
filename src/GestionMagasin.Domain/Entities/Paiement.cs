using GestionMagasin.Domain.Common;
using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Règlement rattaché à une vente. Une même vente peut recevoir plusieurs
/// paiements, éventuellement de modes différents (espèces + carte) ou étalés
/// dans le temps.
/// </summary>
public class Paiement : EntiteBase
{
    public int VenteId { get; set; }

    public Vente Vente { get; set; } = null!;

    public ModePaiement ModePaiement { get; set; }

    public decimal Montant { get; set; }

    public DateTime DatePaiement { get; set; }

    /// <summary>Référence externe : numéro de transaction bancaire, de virement...</summary>
    public string? Reference { get; set; }
}
