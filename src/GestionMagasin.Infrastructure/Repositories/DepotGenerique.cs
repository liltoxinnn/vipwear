using GestionMagasin.Domain.Common;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Infrastructure.Repositories;

/// <summary>
/// Implémentation Entity Framework du dépôt commun à toutes les entités.
/// </summary>
public class DepotGenerique<T> : IDepotGenerique<T> where T : EntiteBase
{
    protected readonly ContexteMagasin Contexte;
    protected readonly DbSet<T> Ensemble;

    public DepotGenerique(ContexteMagasin contexte)
    {
        Contexte = contexte;
        Ensemble = contexte.Set<T>();
    }

    public virtual Task<T?> ObtenirParIdAsync(int id, CancellationToken jeton = default) =>
        Ensemble.FirstOrDefaultAsync(e => e.Id == id, jeton);

    public virtual IQueryable<T> Requete() => Ensemble.AsNoTracking();

    public virtual IQueryable<T> RequeteSuivie() => Ensemble;

    public virtual async Task AjouterAsync(T entite, CancellationToken jeton = default) =>
        await Ensemble.AddAsync(entite, jeton).ConfigureAwait(false);

    public virtual async Task AjouterPlusieursAsync(IEnumerable<T> entites, CancellationToken jeton = default) =>
        await Ensemble.AddRangeAsync(entites, jeton).ConfigureAwait(false);

    public virtual void Modifier(T entite) => Ensemble.Update(entite);

    public virtual void Supprimer(T entite) => Ensemble.Remove(entite);
}
