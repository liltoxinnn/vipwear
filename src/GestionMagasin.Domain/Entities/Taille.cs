using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Taille d'un article (XS, S, M, L, XL, XXL, 38, 40...).
/// <see cref="Ordre"/> permet un classement logique et non alphabétique.
/// </summary>
public class Taille : EntiteBase
{
    public string Nom { get; set; } = string.Empty;

    /// <summary>Position d'affichage : XS avant S, S avant M, etc.</summary>
    public int Ordre { get; set; }

    public bool Actif { get; set; } = true;

    public ICollection<VarianteProduit> Variantes { get; set; } = new List<VarianteProduit>();
}
