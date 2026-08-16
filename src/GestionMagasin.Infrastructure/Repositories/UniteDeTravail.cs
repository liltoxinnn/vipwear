using System.Collections.Concurrent;
using GestionMagasin.Domain.Common;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace GestionMagasin.Infrastructure.Repositories;

/// <summary>
/// Regroupe les dépôts autour d'un même contexte et pilote les transactions.
/// Les services métier peuvent s'appeler entre eux sans se soucier de savoir
/// qui a ouvert la transaction : seule la plus externe valide réellement.
/// </summary>
public class UniteDeTravail : IUniteDeTravail
{
    private readonly ContexteMagasin _contexte;
    private readonly ConcurrentDictionary<Type, object> _depots = new();

    private IDepotVariantes? _variantes;
    private IDepotInventaire? _inventaire;
    private IDepotUtilisateurs? _utilisateurs;

    public UniteDeTravail(ContexteMagasin contexte)
    {
        _contexte = contexte;
    }

    public IDepotVariantes Variantes => _variantes ??= new DepotVariantes(_contexte);

    public IDepotInventaire Inventaire => _inventaire ??= new DepotInventaire(_contexte);

    public IDepotUtilisateurs Utilisateurs => _utilisateurs ??= new DepotUtilisateurs(_contexte);

    public bool TransactionEnCours => _contexte.Database.CurrentTransaction is not null;

    public IDepotGenerique<T> Depot<T>() where T : EntiteBase =>
        (IDepotGenerique<T>)_depots.GetOrAdd(typeof(T), _ => new DepotGenerique<T>(_contexte));

    public Task<int> EnregistrerAsync(CancellationToken jeton = default) =>
        _contexte.SaveChangesAsync(jeton);

    public async Task<ITransactionMetier> DemarrerTransactionAsync(CancellationToken jeton = default)
    {
        if (_contexte.Database.CurrentTransaction is not null)
        {
            // Transaction déjà ouverte par un service appelant : on s'y
            // rattache sans rien valider ni annuler de notre côté.
            return new TransactionImbriquee();
        }

        var transaction = await _contexte.Database.BeginTransactionAsync(jeton).ConfigureAwait(false);

        return new TransactionEntityFramework(transaction);
    }

    public void ViderSuivi() => _contexte.ChangeTracker.Clear();

    /// <summary>Transaction réelle adossée à Entity Framework.</summary>
    private sealed class TransactionEntityFramework : ITransactionMetier
    {
        private readonly IDbContextTransaction _transaction;
        private bool _terminee;

        public TransactionEntityFramework(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task ValiderAsync(CancellationToken jeton = default)
        {
            await _transaction.CommitAsync(jeton).ConfigureAwait(false);
            _terminee = true;
        }

        public async Task AnnulerAsync(CancellationToken jeton = default)
        {
            await _transaction.RollbackAsync(jeton).ConfigureAwait(false);
            _terminee = true;
        }

        public async ValueTask DisposeAsync()
        {
            // Une transaction libérée sans validation explicite est annulée :
            // une exception ne peut jamais laisser des données à moitié écrites.
            if (!_terminee)
            {
                try
                {
                    await _transaction.RollbackAsync().ConfigureAwait(false);
                }
                catch
                {
                    // La connexion peut déjà être perdue : l'annulation est
                    // alors assurée par le serveur lui-même.
                }
            }

            await _transaction.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Jeton sans effet retourné lorsqu'une transaction est déjà ouverte.
    /// Il permet d'écrire tous les services de la même façon.
    /// </summary>
    private sealed class TransactionImbriquee : ITransactionMetier
    {
        public Task ValiderAsync(CancellationToken jeton = default) => Task.CompletedTask;

        public Task AnnulerAsync(CancellationToken jeton = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
