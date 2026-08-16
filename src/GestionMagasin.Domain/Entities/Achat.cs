using GestionMagasin.Domain.Common;
using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Commande passée auprès d'un fournisseur. Le stock n'est impacté qu'au
/// moment de la réception, pas à la saisie de la commande.
/// </summary>
public class Achat : EntiteBase
{
    public int FournisseurId { get; set; }

    public Fournisseur Fournisseur { get; set; } = null!;

    /// <summary>Numéro séquentiel lisible, du type « ACH-2026-000012 ».</summary>
    public string NumeroAchat { get; set; } = string.Empty;

    public DateTime DateAchat { get; set; }

    /// <summary>Date de la réception complète de la marchandise.</summary>
    public DateTime? DateReception { get; set; }

    public decimal SousTotal { get; set; }

    public decimal Remise { get; set; }

    public decimal Total { get; set; }

    public decimal MontantPaye { get; set; }

    /// <summary>Reste dû au fournisseur, recalculé à chaque règlement.</summary>
    public decimal MontantRestant { get; set; }

    public StatutAchat Statut { get; set; } = StatutAchat.EnAttente;

    public string? Notes { get; set; }

    public int UtilisateurId { get; set; }

    public Utilisateur Utilisateur { get; set; } = null!;

    public ICollection<LigneAchat> Lignes { get; set; } = new List<LigneAchat>();
}
