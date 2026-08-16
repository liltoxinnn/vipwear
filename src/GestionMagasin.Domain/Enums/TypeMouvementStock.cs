namespace GestionMagasin.Domain.Enums;

/// <summary>
/// Nature d'un mouvement de stock. Chaque variation de quantité est tracée
/// avec l'un de ces types afin de conserver un historique exploitable.
/// </summary>
public enum TypeMouvementStock
{
    /// <summary>Réception d'une commande fournisseur : le stock augmente.</summary>
    Achat = 1,

    /// <summary>Vente au client : le stock diminue.</summary>
    Vente = 2,

    /// <summary>Retour d'un client : le stock augmente si l'article est revendable.</summary>
    RetourClient = 3,

    /// <summary>Retour vers le fournisseur : le stock diminue.</summary>
    RetourFournisseur = 4,

    /// <summary>Ajustement manuel après inventaire physique.</summary>
    Ajustement = 5,

    /// <summary>Article endommagé retiré du stock vendable.</summary>
    ProduitEndommage = 6,

    /// <summary>Perte, vol ou disparition constatée.</summary>
    Perte = 7,

    /// <summary>Correction d'une erreur de saisie.</summary>
    Correction = 8
}
