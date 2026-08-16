using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>Couleur d'un article, avec son code hexadécimal pour l'affichage.</summary>
public class Couleur : EntiteBase
{
    public string Nom { get; set; } = string.Empty;

    /// <summary>Code hexadécimal du type « #000000 », utilisé par l'interface.</summary>
    public string? CodeCouleur { get; set; }

    public bool Actif { get; set; } = true;

    public ICollection<VarianteProduit> Variantes { get; set; } = new List<VarianteProduit>();
}
