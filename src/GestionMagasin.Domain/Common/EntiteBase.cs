namespace GestionMagasin.Domain.Common;

/// <summary>
/// Classe de base de toutes les entités persistées possédant un identifiant technique.
/// </summary>
public abstract class EntiteBase
{
    public int Id { get; set; }
}
