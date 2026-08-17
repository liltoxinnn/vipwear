using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace GestionMagasin.App.Services;

/// <summary>
/// Fenêtre de saisie accompagnée de sa portée de services. Refermer la portée
/// libère le contexte de données propre à cette fenêtre.
/// </summary>
public sealed class FenetreIsolee<T> : IDisposable where T : Window
{
    private readonly IServiceScope _portee;

    internal FenetreIsolee(IServiceScope portee, T fenetre)
    {
        _portee = portee;
        Fenetre = fenetre;
    }

    public T Fenetre { get; }

    public void Dispose() => _portee.Dispose();
}

/// <summary>Ouvre les fenêtres de saisie du logiciel.</summary>
public interface IFabriqueFenetres
{
    /// <summary>
    /// Construit une fenêtre dans sa propre portée de services. À utiliser
    /// avec « using » : la portée doit être refermée une fois la fenêtre close.
    /// </summary>
    FenetreIsolee<T> Creer<T>() where T : Window;
}

/// <inheritdoc cref="IFabriqueFenetres"/>
public class FabriqueFenetres : IFabriqueFenetres
{
    private readonly IServiceProvider _fournisseur;

    public FabriqueFenetres(IServiceProvider fournisseur)
    {
        _fournisseur = fournisseur;
    }

    /// <summary>
    /// Chaque fenêtre travaille avec son propre contexte de données. Elle
    /// enregistre ainsi ses modifications sans interférer avec l'écran qui
    /// l'a ouverte, et relit des données à jour à chaque ouverture.
    /// </summary>
    public FenetreIsolee<T> Creer<T>() where T : Window
    {
        var portee = _fournisseur.CreateScope();

        try
        {
            return new FenetreIsolee<T>(portee, portee.ServiceProvider.GetRequiredService<T>());
        }
        catch
        {
            portee.Dispose();
            throw;
        }
    }
}
