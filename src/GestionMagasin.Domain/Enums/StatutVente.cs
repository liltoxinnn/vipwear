namespace GestionMagasin.Domain.Enums;

/// <summary>Cycle de vie d'une vente.</summary>
public enum StatutVente
{
    /// <summary>Vente enregistrée mais non intégralement réglée.</summary>
    PartiellementPayee = 1,

    /// <summary>Vente réglée en totalité.</summary>
    Payee = 2,

    /// <summary>Une partie des articles a été retournée.</summary>
    PartiellementRetournee = 3,

    /// <summary>La totalité des articles a été retournée.</summary>
    Retournee = 4,

    /// <summary>Vente annulée : le stock a été réintégré.</summary>
    Annulee = 5
}
