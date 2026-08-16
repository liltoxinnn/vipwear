namespace GestionMagasin.Domain.Enums;

/// <summary>Cycle de vie d'une commande fournisseur.</summary>
public enum StatutAchat
{
    /// <summary>Commande saisie, marchandise non encore reçue : le stock n'est pas impacté.</summary>
    EnAttente = 1,

    /// <summary>Une partie seulement des articles commandés a été réceptionnée.</summary>
    PartiellementRecu = 2,

    /// <summary>Toute la marchandise a été réceptionnée et intégrée au stock.</summary>
    Recu = 3,

    /// <summary>Commande annulée avant réception.</summary>
    Annule = 4
}
