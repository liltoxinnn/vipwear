namespace GestionMagasin.Domain.Enums;

/// <summary>Cycle de vie d'un retour client.</summary>
public enum StatutRetour
{
    /// <summary>Retour enregistré et traité : stock et remboursement appliqués.</summary>
    Valide = 1,

    /// <summary>Retour refusé : aucun impact sur le stock ni sur la caisse.</summary>
    Refuse = 2
}
