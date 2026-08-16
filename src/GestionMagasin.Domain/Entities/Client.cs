using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Client du magasin. Le rattachement d'une vente à un client est facultatif :
/// une vente au comptoir peut rester anonyme.
/// </summary>
public class Client : EntiteBase
{
    public string Nom { get; set; } = string.Empty;

    public string? Prenom { get; set; }

    public string? Telephone { get; set; }

    public string? Email { get; set; }

    public string? Adresse { get; set; }

    public string? Notes { get; set; }

    public DateTime DateCreation { get; set; }

    /// <summary>Un client inactif reste rattaché à son historique d'achats.</summary>
    public bool Actif { get; set; } = true;

    public ICollection<Vente> Ventes { get; set; } = new List<Vente>();

    public string NomComplet => $"{Prenom} {Nom}".Trim();
}
