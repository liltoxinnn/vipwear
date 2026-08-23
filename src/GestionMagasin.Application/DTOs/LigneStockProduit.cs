namespace GestionMagasin.Application.DTOs;

/// <summary>
/// État du stock d'un produit entier, toutes déclinaisons confondues.
///
/// Le stock se compte par déclinaison, et c'est ainsi qu'il doit être
/// corrigé. Mais un catalogue de deux cents produits en quatre couleurs et
/// six tailles fait près de cinq mille lignes : personne n'y lit rien. La
/// liste montre donc un produit par ligne, avec son total ; le détail des
/// tailles et des couleurs s'ouvre au clic.
/// </summary>
public record LigneStockProduit
{
    public int ProduitId { get; init; }

    public string ProduitNom { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public string? Marque { get; init; }

    public int NombreDeclinaisons { get; init; }

    public int NombreCouleurs { get; init; }

    public int NombreTailles { get; init; }

    /// <summary>Quantité cumulée de toutes les déclinaisons.</summary>
    public int StockTotal { get; init; }

    /// <summary>Valeur immobilisée par le produit, au prix d'achat.</summary>
    public decimal ValeurStock { get; init; }

    public int NombreRuptures { get; init; }

    public int NombreStockFaible { get; init; }

    /// <summary>« 2 couleurs · 3 tailles », affiché sous le nom du produit.</summary>
    public string Resume =>
        $"{Accorder(NombreCouleurs, "couleur")} · {Accorder(NombreTailles, "taille")}";

    /// <summary>
    /// Ce qui manque, en clair. Une ligne verte ne dit rien du trou de
    /// deux tailles qu'elle cache : le responsable doit le voir sans ouvrir.
    /// </summary>
    public string LibelleAlerte
    {
        get
        {
            if (NombreRuptures == 0 && NombreStockFaible == 0)
            {
                return string.Empty;
            }

            var parties = new List<string>(2);

            if (NombreRuptures > 0)
            {
                parties.Add(Accorder(NombreRuptures, "rupture"));
            }

            if (NombreStockFaible > 0)
            {
                parties.Add($"{NombreStockFaible} sous le seuil");
            }

            return string.Join(", ", parties);
        }
    }

    /// <summary>
    /// État du produit entier. Un produit dont il reste une seule taille
    /// n'est pas « disponible » : il est incomplet, et se vendra mal.
    /// </summary>
    public string LibelleEtat =>
        StockTotal <= 0 ? "Rupture"
        : NombreRuptures > 0 || NombreStockFaible > 0 ? "Stock faible"
        : "Disponible";

    /// <summary>
    /// Regroupe des déclinaisons par produit. Le regroupement se fait sur la
    /// liste déjà filtrée : chercher les ruptures ne laisse donc que les
    /// produits qui en ont, et n'ouvre que celles-là.
    /// </summary>
    public static IReadOnlyList<LigneStockProduit> Regrouper(IEnumerable<LigneStockDto> lignes) =>
        lignes
            .GroupBy(l => l.ProduitId)
            .Select(produit => new LigneStockProduit
            {
                ProduitId = produit.Key,
                ProduitNom = produit.First().ProduitNom,
                Reference = produit.First().Reference,
                Marque = produit.First().Marque,
                NombreDeclinaisons = produit.Count(),
                NombreCouleurs = produit.Select(l => l.Couleur).Distinct().Count(),
                NombreTailles = produit.Select(l => l.Taille).Distinct().Count(),
                StockTotal = produit.Sum(l => l.QuantiteDisponible),
                ValeurStock = produit.Sum(l => l.ValeurStock),
                NombreRuptures = produit.Count(l => l.EnRupture),
                NombreStockFaible = produit.Count(l => l.StockFaible)
            })
            .OrderBy(p => p.ProduitNom)
            .ThenBy(p => p.Reference)
            .ToList();

    private static string Accorder(int nombre, string mot) =>
        nombre > 1 ? $"{nombre} {mot}s" : $"{nombre} {mot}";
}
