using GestionMagasin.Domain.Common;
using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Reprise d'articles précédemment vendus. Un retour est toujours rattaché à
/// la vente d'origine, ce qui garantit qu'aucun article ne peut être repris
/// en quantité supérieure à celle réellement vendue.
/// </summary>
public class Retour : EntiteBase
{
    /// <summary>Numéro séquentiel lisible, du type « RET-2026-000007 ».</summary>
    public string NumeroRetour { get; set; } = string.Empty;

    public int VenteId { get; set; }

    public Vente Vente { get; set; } = null!;

    /// <summary>Repris de la vente d'origine lorsque celle-ci est nominative.</summary>
    public int? ClientId { get; set; }

    public Client? Client { get; set; }

    public int UtilisateurId { get; set; }

    public Utilisateur Utilisateur { get; set; } = null!;

    public DateTime DateRetour { get; set; }

    public string? Motif { get; set; }

    /// <summary>Montant effectivement remboursé au client, en DZD.</summary>
    public decimal TotalRembourse { get; set; }

    public StatutRetour Statut { get; set; } = StatutRetour.Valide;

    public ICollection<LigneRetour> Lignes { get; set; } = new List<LigneRetour>();
}
