namespace GestionMagasin.Domain.Interfaces;

/// <summary>
/// Fournit l'heure courante. Passer par cette abstraction plutôt que par
/// <c>DateTime.UtcNow</c> rend les règles datées (délai de retour, rapports
/// par période) vérifiables par les tests.
/// </summary>
public interface IFournisseurHorodatage
{
    /// <summary>Instant présent en temps universel, utilisé pour la persistance.</summary>
    DateTime MaintenantUtc { get; }

    /// <summary>Instant présent dans le fuseau du magasin, utilisé pour l'affichage.</summary>
    DateTime MaintenantLocal { get; }
}
