using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Exceptions;

namespace GestionMagasin.Application.Common;

/// <summary>
/// Session en mémoire de l'utilisateur connecté au poste. Elle est alimentée
/// par le service d'authentification et vidée à la déconnexion.
/// </summary>
public class SessionUtilisateur : ISessionUtilisateur
{
    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _verrou = new();

    public int? UtilisateurId { get; private set; }

    public string NomUtilisateur { get; private set; } = string.Empty;

    public string NomComplet { get; private set; } = string.Empty;

    public string NomRole { get; private set; } = string.Empty;

    public bool EstAuthentifie => UtilisateurId.HasValue;

    public int UtilisateurIdObligatoire => UtilisateurId
        ?? throw new ExceptionMetier("Aucun utilisateur n'est connecté. Veuillez vous reconnecter.");

    /// <summary>Événement déclenché à chaque ouverture ou fermeture de session.</summary>
    public event EventHandler? SessionModifiee;

    public bool Possede(string codePermission)
    {
        lock (_verrou)
        {
            return _permissions.Contains(codePermission);
        }
    }

    public void ExigerPermission(string codePermission)
    {
        if (!Possede(codePermission))
        {
            throw new AccesRefuseException(codePermission);
        }
    }

    /// <summary>Ouvre la session après une authentification réussie.</summary>
    public void Ouvrir(Utilisateur utilisateur, IEnumerable<string> permissions)
    {
        lock (_verrou)
        {
            UtilisateurId = utilisateur.Id;
            NomUtilisateur = utilisateur.NomUtilisateur;
            NomComplet = utilisateur.NomComplet;
            NomRole = utilisateur.Role?.Nom ?? string.Empty;

            _permissions.Clear();
            foreach (var permission in permissions)
            {
                _permissions.Add(permission);
            }
        }

        SessionModifiee?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ferme la session et retire toutes les autorisations.</summary>
    public void Fermer()
    {
        lock (_verrou)
        {
            UtilisateurId = null;
            NomUtilisateur = string.Empty;
            NomComplet = string.Empty;
            NomRole = string.Empty;
            _permissions.Clear();
        }

        SessionModifiee?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Liste des permissions actives, utilisée par l'interface pour masquer les actions.</summary>
    public IReadOnlyList<string> PermissionsActives()
    {
        lock (_verrou)
        {
            return _permissions.ToList();
        }
    }
}
