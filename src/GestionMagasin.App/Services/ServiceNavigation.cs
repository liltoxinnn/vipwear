using GestionMagasin.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GestionMagasin.App.Services;

/// <summary>Change la page affichée dans la zone principale de la fenêtre.</summary>
public interface IServiceNavigation
{
    /// <summary>Vue-modèle actuellement affichée.</summary>
    VueModeleBase? PageCourante { get; }

    /// <summary>Déclenché à chaque changement de page.</summary>
    event EventHandler<VueModeleBase>? PageChangee;

    /// <summary>Affiche la page correspondant à la vue-modèle demandée.</summary>
    Task NaviguerAsync<T>() where T : VueModeleBase;

    /// <summary>Recharge les données de la page affichée.</summary>
    Task RafraichirAsync();
}

/// <inheritdoc cref="IServiceNavigation"/>
public class ServiceNavigation : IServiceNavigation, IDisposable
{
    private readonly IServiceProvider _fournisseur;

    /// <summary>Portée de services de l'écran affiché.</summary>
    private IServiceScope? _portee;

    public ServiceNavigation(IServiceProvider fournisseur)
    {
        _fournisseur = fournisseur;
    }

    public VueModeleBase? PageCourante { get; private set; }

    public event EventHandler<VueModeleBase>? PageChangee;

    public async Task NaviguerAsync<T>() where T : VueModeleBase
    {
        // Chaque écran reçoit sa propre portée, donc son propre contexte de
        // données. Sans cela, tous les écrans partageraient le contexte
        // racine pendant toute la durée de vie du logiciel : leurs lectures
        // se gêneraient mutuellement, et les entités suivies s'y
        // accumuleraient sans jamais être libérées.
        var nouvellePortee = _fournisseur.CreateScope();
        var page = nouvellePortee.ServiceProvider.GetRequiredService<T>();

        var precedente = _portee;
        _portee = nouvellePortee;

        PageCourante = page;
        PageChangee?.Invoke(this, page);

        // Les données sont chargées après l'affichage : l'écran apparaît
        // immédiatement et se remplit ensuite, sans impression de blocage.
        await page.ChargerAsync().ConfigureAwait(true);

        // L'écran quitté n'est libéré qu'une fois le nouveau chargé : une
        // lecture encore en cours sur l'ancien peut ainsi se terminer.
        precedente?.Dispose();
    }

    public void Dispose()
    {
        _portee?.Dispose();
        _portee = null;

        GC.SuppressFinalize(this);
    }

    public async Task RafraichirAsync()
    {
        if (PageCourante is not null)
        {
            await PageCourante.ChargerAsync().ConfigureAwait(true);
        }
    }
}
