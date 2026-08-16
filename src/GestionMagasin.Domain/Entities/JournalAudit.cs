using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Trace d'une action importante réalisée dans le logiciel : qui a fait quoi,
/// sur quelle donnée et quand. Sert aux contrôles internes et aux litiges.
/// </summary>
public class JournalAudit : EntiteBase
{
    /// <summary>Nul lorsque l'action précède l'authentification (échec de connexion).</summary>
    public int? UtilisateurId { get; set; }

    public Utilisateur? Utilisateur { get; set; }

    /// <summary>Nature de l'action : Création, Modification, Suppression, Connexion...</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Type d'objet concerné : Produit, Vente, Utilisateur...</summary>
    public string TypeEntite { get; set; } = string.Empty;

    /// <summary>Identifiant de l'objet concerné, lorsqu'il est connu.</summary>
    public int? EntiteId { get; set; }

    /// <summary>Description lisible en français, affichée telle quelle dans le journal.</summary>
    public string Description { get; set; } = string.Empty;

    public DateTime DateCreation { get; set; }
}
