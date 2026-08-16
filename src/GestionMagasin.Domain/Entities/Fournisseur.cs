using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>Fournisseur auprès duquel le magasin s'approvisionne.</summary>
public class Fournisseur : EntiteBase
{
    public string Nom { get; set; } = string.Empty;

    public string? Entreprise { get; set; }

    public string? Telephone { get; set; }

    public string? Email { get; set; }

    public string? Adresse { get; set; }

    public string? Notes { get; set; }

    /// <summary>Un fournisseur inactif reste rattaché à ses achats passés.</summary>
    public bool Actif { get; set; } = true;

    public DateTime DateCreation { get; set; }

    public ICollection<Achat> Achats { get; set; } = new List<Achat>();
}
