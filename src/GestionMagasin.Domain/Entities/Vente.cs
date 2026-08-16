using GestionMagasin.Domain.Common;
using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Vente encaissée en caisse. Une vente validée est définitive : elle n'est
/// jamais supprimée, même si un produit est désactivé par la suite. Une erreur
/// se corrige par un retour ou une annulation, qui laissent tous deux une trace.
/// </summary>
public class Vente : EntiteBase
{
    /// <summary>Numéro séquentiel lisible, du type « VTE-2026-000045 ».</summary>
    public string NumeroVente { get; set; } = string.Empty;

    /// <summary>Facultatif : une vente au comptoir peut ne pas être nominative.</summary>
    public int? ClientId { get; set; }

    public Client? Client { get; set; }

    public int UtilisateurId { get; set; }

    public Utilisateur Utilisateur { get; set; } = null!;

    public DateTime DateVente { get; set; }

    /// <summary>Somme des lignes avant remise globale.</summary>
    public decimal SousTotal { get; set; }

    /// <summary>Remise appliquée sur l'ensemble du ticket.</summary>
    public decimal Remise { get; set; }

    public decimal Total { get; set; }

    public decimal MontantPaye { get; set; }

    public StatutVente Statut { get; set; } = StatutVente.Payee;

    public string? Notes { get; set; }

    public ICollection<LigneVente> Lignes { get; set; } = new List<LigneVente>();

    public ICollection<Paiement> Paiements { get; set; } = new List<Paiement>();

    public ICollection<Retour> Retours { get; set; } = new List<Retour>();

    /// <summary>Reste à encaisser sur cette vente.</summary>
    public decimal MontantRestant => Total - MontantPaye;
}
