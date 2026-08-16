using GestionMagasin.Domain.Entities;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>Informations du magasin utilisées partout dans le logiciel.</summary>
public interface IServiceParametres
{
    /// <summary>
    /// Charge la fiche magasin. Le résultat est conservé en mémoire car il est
    /// consulté à chaque impression de ticket ; il est rafraîchi dès qu'un
    /// enregistrement a lieu.
    /// </summary>
    Task<ParametresMagasin> ObtenirAsync(CancellationToken jeton = default);

    Task<ParametresMagasin> EnregistrerAsync(ParametresMagasin parametres, CancellationToken jeton = default);

    /// <summary>Formate un montant selon la devise du magasin, par exemple « 1 250,00 DA ».</summary>
    Task<string> FormaterMontantAsync(decimal montant, CancellationToken jeton = default);
}
