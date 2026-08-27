namespace GestionMagasin.Application.DTOs;

/// <summary>
/// Un produit du rayon, tel qu'il apparaît en caisse.
///
/// Le stock se compte par déclinaison, mais le client ne demande pas une
/// déclinaison : il demande un pantalon. Un rayon qui affiche une vignette
/// par déclinaison devient illisible dès qu'un produit existe en quatre
/// couleurs et six tailles — vingt-quatre vignettes pour un seul article.
///
/// La caisse montre donc un produit par vignette, et la taille et la couleur
/// se choisissent ensuite, avec leur stock affiché.
/// </summary>
public sealed class ArticleRayon
{
    private ArticleRayon(
        int produitId,
        string nom,
        string? marque,
        string categorie,
        IReadOnlyList<CouleurRayon> couleurs)
    {
        ProduitId = produitId;
        Nom = nom;
        Marque = marque;
        Categorie = categorie;
        Couleurs = couleurs;

        var declinaisons = couleurs.SelectMany(c => c.Tailles).ToList();

        NombreDeclinaisons = declinaisons.Count;
        StockTotal = declinaisons.Sum(d => d.QuantiteDisponible);
        PrixMinimum = declinaisons.Min(d => d.PrixVente);
        PrixMaximum = declinaisons.Max(d => d.PrixVente);
    }

    public int ProduitId { get; }

    public string Nom { get; }

    public string? Marque { get; }

    /// <summary>Famille du produit : chemises, pantalons, chaussures…</summary>
    public string Categorie { get; }

    /// <summary>Déclinaisons regroupées par couleur, tailles en ordre croissant.</summary>
    public IReadOnlyList<CouleurRayon> Couleurs { get; }

    public int NombreDeclinaisons { get; }

    /// <summary>Stock cumulé de toutes les déclinaisons du produit.</summary>
    public int StockTotal { get; }

    public decimal PrixMinimum { get; }

    public decimal PrixMaximum { get; }

    /// <summary>Vrai lorsque toutes les déclinaisons partagent le même prix.</summary>
    public bool PrixUnique => PrixMinimum == PrixMaximum;

    /// <summary>
    /// Vrai lorsque le produit n'a qu'une seule déclinaison : il n'y a alors
    /// rien à choisir, et la vignette l'ajoute directement au panier.
    /// </summary>
    public bool SansChoix => NombreDeclinaisons == 1;

    /// <summary>Nombre maximal de tailles pour une même couleur.</summary>
    public int TaillesParCouleur => Couleurs.Max(c => c.Tailles.Count);

    /// <summary>
    /// Vrai lorsque les couleurs et les tailles tiennent sur la vignette.
    ///
    /// Au-delà, le choix s'ouvre dans un panneau : une chemise en cinq
    /// couleurs et six tailles ferait trente cases sur une vignette large
    /// comme la main, illisibles et impossibles à viser au doigt. En deçà,
    /// tout choisir sur place épargne deux gestes au caissier, et c'est
    /// l'immense majorité des articles d'un magasin.
    /// </summary>
    public bool ChoixSurLaVignette =>
        !SansChoix && Couleurs.Count <= 4 && TaillesParCouleur <= 6;

    /// <summary>Unique déclinaison, lorsqu'il n'y en a qu'une.</summary>
    public VarianteDto Declinaison => Couleurs[0].Tailles[0];

    /// <summary>« 3 couleurs · 6 tailles », affiché sous le nom du produit.</summary>
    public string Resume
    {
        get
        {
            var tailles = Couleurs.SelectMany(c => c.Tailles).Select(t => t.Taille).Distinct().Count();

            var partieCouleurs = Couleurs.Count > 1
                ? $"{Couleurs.Count} couleurs"
                : Couleurs[0].Nom;

            var partieTailles = tailles > 1 ? $"{tailles} tailles" : Couleurs[0].Tailles[0].Taille;

            return $"{partieCouleurs} · {partieTailles}";
        }
    }

    /// <summary>
    /// Regroupe des déclinaisons en produits. Les déclinaisons arrivent à
    /// plat depuis le catalogue ; le regroupement est fait ici plutôt qu'en
    /// base, où il obligerait à un second aller-retour par produit.
    /// </summary>
    public static IReadOnlyList<ArticleRayon> Regrouper(IEnumerable<VarianteDto> declinaisons) =>
        declinaisons
            .GroupBy(v => v.ProduitId)
            .Select(produit => new ArticleRayon(
                produit.Key,
                produit.First().ProduitNom,
                produit.First().Marque,
                produit.First().Categorie,
                produit
                    .GroupBy(v => v.CouleurId)
                    .Select(couleur => new CouleurRayon(
                        couleur.First().Couleur,
                        couleur.First().CodeCouleur,
                        couleur.OrderBy(v => v.OrdreTaille).ThenBy(v => v.Taille).ToList()))
                    .OrderBy(c => c.Nom)
                    .ToList()))
            .OrderBy(a => a.Nom)
            .ToList();
}

/// <summary>Une couleur d'un produit, et les tailles disponibles dans cette couleur.</summary>
public sealed class CouleurRayon
{
    public CouleurRayon(string nom, string? code, IReadOnlyList<VarianteDto> tailles)
    {
        Nom = nom;
        Code = code;
        Tailles = tailles;
    }

    public string Nom { get; }

    /// <summary>Code hexadécimal servant à afficher la pastille.</summary>
    public string? Code { get; }

    /// <summary>Tailles de cette couleur, de la plus petite à la plus grande.</summary>
    public IReadOnlyList<VarianteDto> Tailles { get; }

    /// <summary>Stock cumulé de la couleur, toutes tailles confondues.</summary>
    public int StockTotal => Tailles.Sum(t => t.QuantiteDisponible);
}
