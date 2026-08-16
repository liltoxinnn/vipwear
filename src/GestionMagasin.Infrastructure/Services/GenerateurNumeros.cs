using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Infrastructure.Services;

/// <summary>
/// Attribue les numéros de documents à partir de séquences PostgreSQL.
/// Une séquence délivre une valeur unique même en cas d'appels simultanés et
/// même si la transaction appelante est annulée : deux tickets ne peuvent
/// jamais porter le même numéro.
/// </summary>
public class GenerateurNumeros : IGenerateurNumeros
{
    private readonly ContexteMagasin _contexte;
    private readonly IFournisseurHorodatage _horodatage;

    public GenerateurNumeros(ContexteMagasin contexte, IFournisseurHorodatage horodatage)
    {
        _contexte = contexte;
        _horodatage = horodatage;
    }

    public Task<string> ProchainNumeroVenteAsync(CancellationToken jeton = default) =>
        ConstruireAsync("VTE", SequencesDocuments.Ventes, jeton);

    public Task<string> ProchainNumeroAchatAsync(CancellationToken jeton = default) =>
        ConstruireAsync("ACH", SequencesDocuments.Achats, jeton);

    public Task<string> ProchainNumeroRetourAsync(CancellationToken jeton = default) =>
        ConstruireAsync("RET", SequencesDocuments.Retours, jeton);

    private async Task<string> ConstruireAsync(string prefixe, string sequence, CancellationToken jeton)
    {
        var valeur = await ObtenirProchaineValeurAsync(sequence, jeton).ConfigureAwait(false);
        var annee = _horodatage.MaintenantLocal.Year;

        return $"{prefixe}-{annee}-{valeur:D6}";
    }

    private async Task<long> ObtenirProchaineValeurAsync(string sequence, CancellationToken jeton)
    {
        // Le nom de la séquence est transmis en paramètre : aucune valeur
        // saisie par un utilisateur n'entre dans la requête.
        var valeurs = await _contexte.Database
            .SqlQuery<long>($"SELECT nextval(CAST({sequence} AS regclass)) AS \"Value\"")
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return valeurs[0];
    }
}
