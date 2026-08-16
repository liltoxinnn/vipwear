namespace GestionMagasin.Domain.Interfaces;

/// <summary>
/// Attribue les numéros de documents (ventes, achats, retours).
/// L'implémentation s'appuie sur des séquences PostgreSQL : deux caisses
/// simultanées ne peuvent jamais obtenir le même numéro.
/// </summary>
public interface IGenerateurNumeros
{
    /// <summary>Retourne un numéro de vente du type « VTE-2026-000045 ».</summary>
    Task<string> ProchainNumeroVenteAsync(CancellationToken jeton = default);

    /// <summary>Retourne un numéro d'achat du type « ACH-2026-000012 ».</summary>
    Task<string> ProchainNumeroAchatAsync(CancellationToken jeton = default);

    /// <summary>Retourne un numéro de retour du type « RET-2026-000007 ».</summary>
    Task<string> ProchainNumeroRetourAsync(CancellationToken jeton = default);
}
