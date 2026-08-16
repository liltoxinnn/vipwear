using GestionMagasin.Application.Common;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Interfaces;

namespace GestionMagasin.Application.Services;

/// <inheritdoc cref="IServiceAudit"/>
public class ServiceAudit : IServiceAudit
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly ISessionUtilisateur _session;
    private readonly IFournisseurHorodatage _horodatage;

    public ServiceAudit(
        IUniteDeTravail uniteDeTravail,
        ISessionUtilisateur session,
        IFournisseurHorodatage horodatage)
    {
        _uniteDeTravail = uniteDeTravail;
        _session = session;
        _horodatage = horodatage;
    }

    public async Task TracerAsync(
        string action,
        string typeEntite,
        int? entiteId,
        string description,
        CancellationToken jeton = default)
    {
        var entree = new JournalAudit
        {
            UtilisateurId = _session.UtilisateurId,
            Action = action,
            TypeEntite = typeEntite,
            EntiteId = entiteId,
            Description = Tronquer(description, 1000),
            DateCreation = _horodatage.MaintenantUtc
        };

        await _uniteDeTravail.Depot<JournalAudit>().AjouterAsync(entree, jeton).ConfigureAwait(false);
    }

    public async Task TracerEtEnregistrerAsync(
        string action,
        string typeEntite,
        int? entiteId,
        string description,
        CancellationToken jeton = default)
    {
        await TracerAsync(action, typeEntite, entiteId, description, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
    }

    /// <summary>Évite qu'une description trop longue ne fasse échouer l'écriture.</summary>
    private static string Tronquer(string texte, int longueurMaximale) =>
        texte.Length <= longueurMaximale ? texte : texte[..(longueurMaximale - 1)] + "…";
}
