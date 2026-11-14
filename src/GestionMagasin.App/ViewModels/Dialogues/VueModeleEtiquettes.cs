using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Impression;
using GestionMagasin.App.Services;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Impression;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels.Dialogues;

/// <summary>Article de la liste, avec le nombre d'étiquettes à lui imprimer.</summary>
public partial class LigneEtiquette : ObservableObject
{
    /// <summary>Au-delà, c'est une faute de frappe.</summary>
    public const int NombreMaximal = 999;

    public required int VarianteId { get; init; }

    public required string Nom { get; init; }

    public required string Taille { get; init; }

    public required string Couleur { get; init; }

    public required string? CodeBarres { get; init; }

    public required decimal Prix { get; init; }

    public required int Stock { get; init; }

    /// <summary>Taille et couleur, telles qu'imprimées.</summary>
    public string Detail => CompositionEtiquette.Detail(Taille, Couleur);

    public ArticleEtiquette Article => new(Nom, Detail, Prix, CodeBarres ?? string.Empty);

    [ObservableProperty]
    private int _nombre;

    /// <summary>Une étiquette de plus pour cet article.</summary>
    [RelayCommand]
    private void Plus() => Nombre++;

    /// <summary>Une étiquette de moins pour cet article, jamais en dessous de zéro.</summary>
    [RelayCommand]
    private void Moins() => Nombre = Math.Max(0, Nombre - 1);

    partial void OnNombreChanged(int value)
    {
        if (value < 0)
        {
            Nombre = 0;
        }
        else if (value > NombreMaximal)
        {
            Nombre = NombreMaximal;
        }
    }
}

/// <summary>
/// Impression des étiquettes code-barres des déclinaisons d'un produit.
///
/// Tout se règle ici, au moment où l'on imprime : l'imprimante, la taille des
/// étiquettes et ce qu'elles portent. Le choix est retenu pour le poste.
/// </summary>
public partial class VueModeleEtiquettes : VueModeleBase
{
    private readonly IServiceImpressionEtiquettes _impression;

    /// <summary>Numéro de la dernière demande d'aperçu : seule la plus récente s'affiche.</summary>
    private int _apercuDemande;

    public VueModeleEtiquettes(
        IServiceImpressionEtiquettes impression,
        IServiceDialogue dialogue,
        ILogger<VueModeleEtiquettes> journal)
        : base(dialogue, journal)
    {
        _impression = impression;
    }

    public override string Titre => "Étiquettes code-barres";

    public ObservableCollection<LigneEtiquette> Lignes { get; } = [];

    public ObservableCollection<string> Imprimantes { get; } = [];

    [ObservableProperty]
    private string _nomProduit = string.Empty;

    [ObservableProperty]
    private LigneEtiquette? _ligneSelectionnee;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EtatImprimante))]
    private string? _imprimanteChoisie;

    [ObservableProperty]
    private int _largeurMm = ReglagesEtiquettes.LargeurParDefaut;

    [ObservableProperty]
    private int _hauteurMm = ReglagesEtiquettes.HauteurParDefaut;

    [ObservableProperty]
    private bool _afficherPrix = true;

    [ObservableProperty]
    private bool _afficherMagasin = true;

    /// <summary>Nombre à donner d'un coup à tous les articles.</summary>
    [ObservableProperty]
    private int _nombrePourTous = 1;

    [ObservableProperty]
    private BitmapSource? _apercu;

    /// <summary>Pourquoi l'étiquette ne peut pas être imprimée, s'il y a lieu.</summary>
    [ObservableProperty]
    private string? _problemeApercu;

    /// <summary>Nombre total d'étiquettes demandées.</summary>
    public int Total => Lignes.Sum(l => l.Nombre);

    public string LibelleImprimer => Total switch
    {
        0 => "Imprimer",
        1 => "Imprimer 1 étiquette",
        _ => $"Imprimer {Total} étiquettes"
    };

    public string EtatImprimante => Imprimantes.Count == 0
        ? "Windows ne signale aucune imprimante. Installez le pilote de l'imprimante d'étiquettes, puis appuyez sur Actualiser."
        : ImprimanteChoisie is null
            ? "Choisissez l'imprimante d'étiquettes. Le choix est retenu pour ce poste."
            : "Réglez la même taille d'étiquette dans les préférences de l'imprimante, dans Windows.";

    /// <summary>Ce qui a été imprimé, une fois la fenêtre fermée.</summary>
    public string? CompteRendu { get; private set; }

    public event EventHandler<bool>? FermetureDemandee;

    /// <summary>Prépare la liste des déclinaisons à étiqueter.</summary>
    /// <param name="nomProduit">Produit affiché en tête de fenêtre.</param>
    /// <param name="variantes">Déclinaisons proposées ; les désactivées sont écartées.</param>
    /// <param name="nombres">
    /// Nombre d'étiquettes proposé par déclinaison. À défaut, une par article
    /// en stock : c'est ce qu'il faut pour étiqueter le rayon.
    /// </param>
    public void Preparer(
        string nomProduit,
        IEnumerable<VarianteDto> variantes,
        IReadOnlyDictionary<int, int>? nombres = null)
    {
        NomProduit = nomProduit;

        var reglages = _impression.Reglages;

        LargeurMm = reglages.LargeurMm;
        HauteurMm = reglages.HauteurMm;
        AfficherPrix = reglages.AfficherPrix;
        AfficherMagasin = reglages.AfficherMagasin;

        RemplirImprimantes(reglages.NomImprimante);
        ImprimanteChoisie = reglages.NomImprimante;

        foreach (var ligne in Lignes)
        {
            ligne.PropertyChanged -= SurLigneModifiee;
        }

        Lignes.Clear();

        foreach (var variante in variantes.Where(v => v.Actif).OrderBy(v => v.Couleur).ThenBy(v => v.OrdreTaille))
        {
            var ligne = new LigneEtiquette
            {
                VarianteId = variante.Id,
                Nom = variante.ProduitNom,
                Taille = variante.Taille,
                Couleur = variante.Couleur,
                CodeBarres = variante.CodeBarres,
                Prix = variante.PrixVente,
                Stock = variante.QuantiteDisponible,
                Nombre = nombres is not null
                    ? nombres.GetValueOrDefault(variante.Id)
                    : Math.Max(0, variante.QuantiteDisponible)
            };

            ligne.PropertyChanged += SurLigneModifiee;
            Lignes.Add(ligne);
        }

        LigneSelectionnee = Lignes.FirstOrDefault(l => l.Nombre > 0) ?? Lignes.FirstOrDefault();

        SignalerTotal();
    }

    private void SurLigneModifiee(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LigneEtiquette.Nombre))
        {
            SignalerTotal();
        }
    }

    private void SignalerTotal()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(LibelleImprimer));
    }

    private void RemplirImprimantes(string? aConserver)
    {
        Imprimantes.Clear();

        foreach (var nom in _impression.ListerImprimantes())
        {
            Imprimantes.Add(nom);
        }

        if (aConserver is not null && !Imprimantes.Contains(aConserver))
        {
            Imprimantes.Add(aConserver);
        }

        OnPropertyChanged(nameof(EtatImprimante));
    }

    private ReglagesEtiquettes ReglagesSaisis() => new ReglagesEtiquettes
    {
        NomImprimante = ImprimanteChoisie,
        LargeurMm = LargeurMm,
        HauteurMm = HauteurMm,
        AfficherPrix = AfficherPrix,
        AfficherMagasin = AfficherMagasin
    }.Normaliser();

    // ------------------------------------------------------------------
    // Aperçu
    // ------------------------------------------------------------------

    partial void OnLigneSelectionneeChanged(LigneEtiquette? value) => _ = ActualiserApercuAsync();

    partial void OnLargeurMmChanged(int value) => _ = ActualiserApercuAsync();

    partial void OnHauteurMmChanged(int value) => _ = ActualiserApercuAsync();

    partial void OnAfficherPrixChanged(bool value) => _ = ActualiserApercuAsync();

    partial void OnAfficherMagasinChanged(bool value) => _ = ActualiserApercuAsync();

    /// <summary>
    /// Redessine l'étiquette de l'article sélectionné. Une étiquette
    /// impossible ne déclenche pas de message : la raison s'affiche à la
    /// place de l'aperçu, pendant qu'on règle la taille.
    /// </summary>
    private async Task ActualiserApercuAsync()
    {
        var demande = ++_apercuDemande;
        var ligne = LigneSelectionnee;

        if (ligne is null)
        {
            Apercu = null;
            ProblemeApercu = null;
            return;
        }

        BitmapSource? image = null;
        string? probleme = null;

        try
        {
            image = await _impression.ApercuAsync(ligne.Article, ReglagesSaisis()).ConfigureAwait(true);
        }
        catch (EtiquetteImpossibleException erreur)
        {
            probleme = erreur.Message;
        }
        catch (Exception erreur) when (erreur is not OutOfMemoryException)
        {
            Journal.LogWarning(erreur, "Aperçu d'étiquette impossible.");
            probleme = "L'aperçu n'a pas pu être dessiné.";
        }

        if (demande != _apercuDemande)
        {
            return;
        }

        Apercu = image;
        ProblemeApercu = probleme;
    }

    // ------------------------------------------------------------------
    // Commandes
    // ------------------------------------------------------------------

    [RelayCommand]
    private void ActualiserImprimantes()
    {
        var choisie = ImprimanteChoisie;

        RemplirImprimantes(choisie);
        ImprimanteChoisie = choisie;
    }

    /// <summary>Une étiquette par article en stock : pour étiqueter le rayon.</summary>
    [RelayCommand]
    private void UneParArticleEnStock()
    {
        foreach (var ligne in Lignes)
        {
            ligne.Nombre = Math.Max(0, ligne.Stock);
        }
    }

    [RelayCommand]
    private void UneDeChaque()
    {
        foreach (var ligne in Lignes)
        {
            ligne.Nombre = 1;
        }
    }

    /// <summary>Le même nombre d'étiquettes pour tous les articles.</summary>
    [RelayCommand]
    private void AppliquerATous()
    {
        var nombre = Math.Clamp(NombrePourTous, 0, LigneEtiquette.NombreMaximal);

        foreach (var ligne in Lignes)
        {
            ligne.Nombre = nombre;
        }
    }

    [RelayCommand]
    private void Aucune()
    {
        foreach (var ligne in Lignes)
        {
            ligne.Nombre = 0;
        }
    }

    [RelayCommand]
    private async Task ImprimerAsync()
    {
        if (ImprimanteChoisie is null)
        {
            Dialogue.Avertir(
                "Choisissez d'abord l'imprimante d'étiquettes dans la liste.",
                "Aucune imprimante");

            return;
        }

        if (Total == 0)
        {
            Dialogue.Avertir(
                "Indiquez, dans la colonne « Étiquettes », combien en imprimer pour au moins un article.",
                "Aucune étiquette demandée");

            return;
        }

        var reglages = ReglagesSaisis();

        var demandes = Lignes
            .Where(l => l.Nombre > 0)
            .Select(l => new DemandeEtiquettes(l.Article, l.Nombre))
            .ToList();

        var imprimees = 0;

        EstOccupe = true;

        try
        {
            imprimees = await _impression.ImprimerAsync(demandes, reglages).ConfigureAwait(true);
        }
        catch (Exception erreur) when (erreur is ImpressionEtiquettesException or EtiquetteImpossibleException)
        {
            Journal.LogWarning(erreur, "Étiquettes non imprimées.");
            Dialogue.Avertir(erreur.Message, "Étiquettes non imprimées");

            return;
        }
        catch (Exception erreur) when (erreur is not OutOfMemoryException)
        {
            Journal.LogError(erreur, "Étiquettes : incident pendant l'impression.");
            Dialogue.Avertir(
                "Les étiquettes n'ont pas pu être imprimées. Vérifiez que l'imprimante est allumée, " +
                "branchée et approvisionnée en étiquettes.",
                "Étiquettes non imprimées");

            return;
        }
        finally
        {
            EstOccupe = false;
        }

        // L'imprimante et la taille qui viennent de servir sont retenues :
        // la prochaine fois, il n'y aura qu'à appuyer sur Imprimer.
        try
        {
            _impression.Enregistrer(reglages);
        }
        catch (Exception erreur) when (erreur is IOException or UnauthorizedAccessException)
        {
            Journal.LogWarning(erreur, "Réglages des étiquettes non enregistrés.");
        }

        // Pas de message à valider : la fenêtre se ferme, et l'écran qui l'a
        // ouverte affiche ce compte rendu dans sa barre d'état.
        CompteRendu = imprimees == 1
            ? $"1 étiquette envoyée à « {ImprimanteChoisie} »."
            : $"{imprimees} étiquettes envoyées à « {ImprimanteChoisie} ».";

        FermetureDemandee?.Invoke(this, true);
    }

    [RelayCommand]
    private void Annuler() => FermetureDemandee?.Invoke(this, false);
}
