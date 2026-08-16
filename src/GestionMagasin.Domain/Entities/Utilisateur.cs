using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Compte permettant de se connecter au logiciel.
/// Le mot de passe n'est jamais conservé en clair : seule son empreinte
/// PBKDF2 salée est stockée dans <see cref="PasswordHash"/>.
/// </summary>
public class Utilisateur : EntiteBase
{
    public string Nom { get; set; } = string.Empty;

    public string Prenom { get; set; } = string.Empty;

    public string NomUtilisateur { get; set; } = string.Empty;

    /// <summary>Empreinte du mot de passe au format « algorithme$itérations$sel$empreinte ».</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public bool Actif { get; set; } = true;

    public DateTime DateCreation { get; set; }

    public DateTime? DerniereConnexion { get; set; }

    /// <summary>Nom complet affiché dans l'interface et sur les documents imprimés.</summary>
    public string NomComplet => $"{Prenom} {Nom}".Trim();
}
