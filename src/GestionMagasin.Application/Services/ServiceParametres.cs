using GestionMagasin.Application.Common;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Domain.Securite;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Application.Services;

/// <inheritdoc cref="IServiceParametres"/>
public class ServiceParametres : IServiceParametres
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IServiceAudit _audit;
    private readonly ISessionUtilisateur _session;
    private readonly IFournisseurHorodatage _horodatage;

    private ParametresMagasin? _cache;
    private readonly SemaphoreSlim _verrou = new(1, 1);

    public ServiceParametres(
        IUniteDeTravail uniteDeTravail,
        IServiceAudit audit,
        ISessionUtilisateur session,
        IFournisseurHorodatage horodatage)
    {
        _uniteDeTravail = uniteDeTravail;
        _audit = audit;
        _session = session;
        _horodatage = horodatage;
    }

    public async Task<ParametresMagasin> ObtenirAsync(CancellationToken jeton = default)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _verrou.WaitAsync(jeton).ConfigureAwait(false);

        try
        {
            _cache ??= await _uniteDeTravail.Depot<ParametresMagasin>().Requete()
                .FirstOrDefaultAsync(jeton)
                .ConfigureAwait(false)
                ?? new ParametresMagasin { Id = 1 };

            return _cache;
        }
        finally
        {
            _verrou.Release();
        }
    }

    public async Task<ParametresMagasin> EnregistrerAsync(
        ParametresMagasin parametres,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererParametres);

        Valider(parametres);

        var existants = await _uniteDeTravail.Depot<ParametresMagasin>().RequeteSuivie()
            .FirstOrDefaultAsync(jeton)
            .ConfigureAwait(false);

        if (existants is null)
        {
            parametres.Id = 1;
            parametres.DateModification = _horodatage.MaintenantUtc;

            await _uniteDeTravail.Depot<ParametresMagasin>().AjouterAsync(parametres, jeton).ConfigureAwait(false);
        }
        else
        {
            existants.NomMagasin = parametres.NomMagasin.Trim();
            existants.Logo = parametres.Logo;
            existants.Adresse = parametres.Adresse?.Trim();
            existants.Telephone = parametres.Telephone?.Trim();
            existants.Email = parametres.Email?.Trim();
            existants.Devise = parametres.Devise.Trim();
            existants.SymboleDevise = parametres.SymboleDevise.Trim();
            existants.InformationsTicket = parametres.InformationsTicket?.Trim();
            existants.RegistreCommerce = parametres.RegistreCommerce?.Trim();
            existants.NumeroIdentificationFiscale = parametres.NumeroIdentificationFiscale?.Trim();
            existants.ArticleImposition = parametres.ArticleImposition?.Trim();
            existants.TauxTva = parametres.TauxTva;
            existants.DelaiRetourJours = parametres.DelaiRetourJours;
            existants.DateModification = _horodatage.MaintenantUtc;
        }

        await _audit.TracerAsync(
            ActionsAudit.Modification,
            nameof(ParametresMagasin),
            1,
            "Paramètres du magasin modifiés.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        // Le cache est vidé pour que les prochains tickets reprennent
        // immédiatement les nouvelles informations.
        _cache = null;

        return await ObtenirAsync(jeton).ConfigureAwait(false);
    }

    public async Task<string> FormaterMontantAsync(decimal montant, CancellationToken jeton = default)
    {
        var parametres = await ObtenirAsync(jeton).ConfigureAwait(false);

        return FormatageMontant.Formater(montant, parametres.SymboleDevise);
    }

    private static void Valider(ParametresMagasin parametres)
    {
        var erreurs = new List<string>();

        if (string.IsNullOrWhiteSpace(parametres.NomMagasin))
        {
            erreurs.Add("Le nom du magasin est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(parametres.Devise))
        {
            erreurs.Add("La devise est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(parametres.SymboleDevise))
        {
            erreurs.Add("Le symbole de la devise est obligatoire.");
        }

        if (parametres.TauxTva is < 0 or > 100)
        {
            erreurs.Add("Le taux de TVA doit être compris entre 0 et 100 %.");
        }

        if (parametres.DelaiRetourJours < 0)
        {
            erreurs.Add("Le délai de retour ne peut pas être négatif.");
        }

        if (!string.IsNullOrWhiteSpace(parametres.Email) && !parametres.Email.Contains('@'))
        {
            erreurs.Add("L'adresse e-mail du magasin n'est pas valide.");
        }

        if (erreurs.Count > 0)
        {
            throw new ValidationMetierException(erreurs);
        }
    }
}
