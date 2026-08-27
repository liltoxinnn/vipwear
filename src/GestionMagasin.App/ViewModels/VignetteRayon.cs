using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GestionMagasin.Application.DTOs;

namespace GestionMagasin.App.ViewModels;

/// <summary>
/// Pastille de couleur sur une vignette du rayon.
///
/// La sélection est portée ici et non sur <see cref="CouleurRayon"/> : ce
/// dernier est une donnée calculée, partagée et testée, qui n'a pas à savoir
/// sur quoi le caissier vient d'appuyer. Une seule pastille reste allumée à
/// la fois, et c'est la vignette qui l'impose.
/// </summary>
public partial class PastilleCouleur : ObservableObject
{
    private readonly VignetteRayon _vignette;

    internal PastilleCouleur(VignetteRayon vignette, CouleurRayon couleur, bool choisie)
    {
        _vignette = vignette;
        Couleur = couleur;
        _estChoisie = choisie;

        // Un groupe par produit. Sans cette distinction, toutes les pastilles
        // du rayon appartiendraient au même groupe et choisir une couleur sur
        // un article en éteindrait une sur tous les autres.
        Groupe = "vignette-" + vignette.ProduitId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Groupe de sélection, propre à la vignette.</summary>
    public string Groupe { get; }

    public CouleurRayon Couleur { get; }

    public string Nom => Couleur.Nom;

    public string? Code => Couleur.Code;

    /// <summary>Vrai lorsque la couleur est en rupture sur toutes ses tailles.</summary>
    public bool Epuisee => Couleur.StockTotal <= 0;

    [ObservableProperty]
    private bool _estChoisie;

    partial void OnEstChoisieChanged(bool value)
    {
        if (value)
        {
            _vignette.ChoisirCouleur(this);
        }
    }
}

/// <summary>
/// Vignette d'un produit dans le rayon de la caisse.
///
/// Le regroupement des déclinaisons est un calcul, et il vit dans la couche
/// applicative où il se teste. Ce qui vit ici est ce qu'il ne peut pas
/// porter : la couleur que le caissier vient de désigner du doigt, et la
/// liste des tailles qui en découle. C'est un état d'affichage, propre à
/// une vignette et à un instant.
/// </summary>
public partial class VignetteRayon : ObservableObject
{
    public VignetteRayon(ArticleRayon article)
    {
        Article = article;

        // La première couleur encore disponible est présélectionnée : le
        // caissier n'a qu'un geste à faire pour le cas courant, et un second
        // seulement s'il veut une autre couleur.
        var depart = article.Couleurs.FirstOrDefault(c => c.StockTotal > 0) ?? article.Couleurs[0];

        foreach (var couleur in article.Couleurs)
        {
            Couleurs.Add(new PastilleCouleur(this, couleur, couleur == depart));
        }

        RemplirTailles(depart);
    }

    public ArticleRayon Article { get; }

    public int ProduitId => Article.ProduitId;

    public string Nom => Article.Nom;

    public string Resume => Article.Resume;

    public decimal PrixMinimum => Article.PrixMinimum;

    public bool PrixUnique => Article.PrixUnique;

    public int StockTotal => Article.StockTotal;

    /// <summary>Vrai lorsque couleurs et tailles tiennent sur la vignette.</summary>
    public bool ChoixSurLaVignette => Article.ChoixSurLaVignette;

    /// <summary>Vrai lorsqu'il faut ouvrir un panneau pour choisir.</summary>
    public bool ChoixDansUnPanneau => !Article.ChoixSurLaVignette && !Article.SansChoix;

    /// <summary>Vrai lorsqu'il n'y a rien à choisir : une seule déclinaison.</summary>
    public bool SansChoix => Article.SansChoix;

    /// <summary>Vrai lorsque le produit existe en plusieurs couleurs.</summary>
    public bool PlusieursCouleurs => Article.Couleurs.Count > 1;

    /// <summary>Unique déclinaison, lorsqu'il n'y en a qu'une.</summary>
    public VarianteDto Declinaison => Article.Declinaison;

    public ObservableCollection<PastilleCouleur> Couleurs { get; } = [];

    /// <summary>Tailles de la couleur retenue, dans l'ordre croissant.</summary>
    public ObservableCollection<VarianteDto> Tailles { get; } = [];

    /// <summary>
    /// Retient une couleur. Les tailles affichées la suivent : la même
    /// lettre sur une autre couleur est une autre déclinaison, avec son
    /// propre stock.
    ///
    /// Éteindre les autres pastilles n'incombe pas à ce code : les boutons
    /// forment un groupe, et un groupe n'en laisse jamais deux allumés — ni
    /// aucun. Appuyer sur la couleur déjà retenue ne la décoche donc pas, et
    /// la vignette garde toujours des tailles à vendre.
    /// </summary>
    internal void ChoisirCouleur(PastilleCouleur pastille) => RemplirTailles(pastille.Couleur);

    private void RemplirTailles(CouleurRayon couleur)
    {
        Tailles.Clear();

        foreach (var taille in couleur.Tailles)
        {
            Tailles.Add(taille);
        }
    }

    /// <summary>Regroupe des déclinaisons en vignettes prêtes à afficher.</summary>
    public static IEnumerable<VignetteRayon> Composer(IEnumerable<VarianteDto> declinaisons) =>
        ArticleRayon.Regrouper(declinaisons).Select(a => new VignetteRayon(a));
}
