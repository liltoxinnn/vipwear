using GestionMagasin.Domain.Entities;

namespace GestionMagasin.Application.Common;

/// <summary>
/// Conserve la fiche magasin en mémoire pour l'ensemble de l'application.
/// Cette fiche est lue à chaque impression de ticket mais ne change que très
/// rarement : la garder ici évite une requête inutile à chaque encaissement.
///
/// Le cache est un composant partagé distinct du service, afin que celui-ci
/// reste de portée limitée et n'immobilise jamais une connexion à la base.
/// </summary>
public class CacheParametresMagasin
{
    private readonly Lock _verrou = new();
    private ParametresMagasin? _parametres;

    public ParametresMagasin? Lire()
    {
        lock (_verrou)
        {
            return _parametres;
        }
    }

    public void Enregistrer(ParametresMagasin parametres)
    {
        lock (_verrou)
        {
            _parametres = parametres;
        }
    }

    /// <summary>Vide le cache après une modification des paramètres.</summary>
    public void Vider()
    {
        lock (_verrou)
        {
            _parametres = null;
        }
    }
}
