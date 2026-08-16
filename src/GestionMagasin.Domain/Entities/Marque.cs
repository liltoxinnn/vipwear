using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>Marque commerciale d'un produit (Nike, Adidas, Zara...).</summary>
public class Marque : EntiteBase
{
    public string Nom { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool Actif { get; set; } = true;

    public ICollection<Produit> Produits { get; set; } = new List<Produit>();
}
