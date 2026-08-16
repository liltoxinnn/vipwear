namespace GestionMagasin.Domain.Enums;

/// <summary>
/// État physique d'un article rapporté par le client. Détermine si l'article
/// peut réintégrer le stock vendable.
/// </summary>
public enum EtatArticleRetour
{
    /// <summary>Article en bon état : il réintègre le stock disponible.</summary>
    Revendable = 1,

    /// <summary>Article abîmé : il ne réintègre pas le stock disponible.</summary>
    Endommage = 2
}
