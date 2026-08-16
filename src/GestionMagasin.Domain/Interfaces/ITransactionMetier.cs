namespace GestionMagasin.Domain.Interfaces;

/// <summary>
/// Transaction de base de données vue par la couche métier, sans dépendance
/// à Entity Framework. Tant que <see cref="ValiderAsync"/> n'a pas été appelé,
/// aucune modification n'est définitive : libérer l'objet annule l'ensemble.
/// </summary>
public interface ITransactionMetier : IAsyncDisposable
{
    /// <summary>Rend définitives toutes les modifications de la transaction.</summary>
    Task ValiderAsync(CancellationToken jeton = default);

    /// <summary>Annule l'intégralité des modifications de la transaction.</summary>
    Task AnnulerAsync(CancellationToken jeton = default);
}
