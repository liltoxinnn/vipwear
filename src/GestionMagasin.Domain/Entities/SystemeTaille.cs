using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Système de tailles : la façon dont une famille d'articles se mesure.
///
/// Une chemise se vend en S, M, L ; un pantalon en 38, 40, 42 ; une
/// chaussure en pointures. Proposer les trois séries mélangées à la création
/// d'un article, c'est laisser créer une chaussure en XXL — une déclinaison
/// que personne ne vendra jamais et qui polluera le stock pour de bon.
///
/// Le système est partagé entre catégories : chemises, t-shirts et vestes se
/// mesurent tous en lettres, et n'ont aucune raison d'entretenir chacun sa
/// propre liste.
/// </summary>
public class SystemeTaille : EntiteBase
{
    public string Nom { get; set; } = string.Empty;

    /// <summary>Position d'affichage dans les listes.</summary>
    public int Ordre { get; set; }

    public bool Actif { get; set; } = true;

    public ICollection<Taille> Tailles { get; set; } = new List<Taille>();

    public ICollection<Categorie> Categories { get; set; } = new List<Categorie>();
}
