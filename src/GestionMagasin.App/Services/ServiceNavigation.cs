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
public class ServiceNavigation : IServiceNavigation
{
    private readonly IServiceProvider _fournisseur;

    public ServiceNavigation(IServiceProvider fournisseur)
    {
        _fournisseur = fournisseur;
    }

    public VueModeleBase? PageCourante { get; private set; }

    public event EventHandler<VueModeleBase>? PageChangee;

    public async Task NaviguerAsync<T>() where T : VueModeleBase
    {
        var page = _fournisseur.GetRequiredService<T>();

        PageCourante = page;
        PageChangee?.Invoke(this, page);

        // Les données sont chargées après l'affichage : l'écran apparaît
        // immédiatement et se remplit ensuite, sans impression de blocage.
        await page.ChargerAsync().ConfigureAwait(true);
    }

    public async Task RafraichirAsync()
    {
        if (PageCourante is not null)
        {
            await PageCourante.ChargerAsync().ConfigureAwait(true);
        }
    }
}
