using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels.Dialogues;

/// <summary>Sens d'un mouvement de stock saisi manuellement.</summary>
public record SensMouvement(bool EstEntree, string Libelle);

/// <summary>
/// Saisie d'un mouvement de stock manuel ou d'un ajustement d'inventaire.
/// Toutes les écritures passent par le service de stock centralisé.
/// </summary>
public partial class VueModeleMouvementStock : VueModeleBase
{
    private readonly IServiceStock _stock;

    public VueModeleMouvementStock(
        IServiceStock stock,
        IServiceDialogue dialogue,
        ILogger<VueModeleMouvementStock> journal)
        : base(dialogue, journal)
    {
        _stock = stock;
        _sensChoisi = Sens[0];
        _typeMouvement = TypeMouvementStock.Correction;
    }

    public override string Titre => EstAjustement
        ? "Ajustement d'inventaire"
        : "Nouveau mouvement de stock";

    public IReadOnlyList<SensMouvement> Sens { get; } =
    [
        new(true, "Entrée de stock"),
        new(false, "Sortie de stock")
    ];

    /// <summary>Types proposés selon le sens choisi.</summary>
    public IReadOnlyList<TypeMouvementStock> TypesMouvement { get; } =
    [
        TypeMouvementStock.Correction,
        TypeMouvementStock.Perte,
        TypeMouvementStock.ProduitEndommage,
        TypeMouvementStock.RetourFournisseur,
        TypeMouvementStock.Ajustement
    ];

    [ObservableProperty]
    private bool _estAjustement;

    [ObservableProperty]
    private int _varianteProduitId;

    [ObservableProperty]
    private string _designation = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private int _stockActuel;

    // Nullable à dessein : en refermant la fenêtre, WPF vide la sélection de
    // la liste déroulante et réécrit « rien » ici. Tout ce qui lit le sens du
    // mouvement doit donc supporter cette valeur.
    [ObservableProperty]
    private SensMouvement? _sensChoisi;

    [ObservableProperty]
    private TypeMouvementStock _typeMouvement;

    [ObservableProperty]
    private int _quantite = 1;

    [ObservableProperty]
    private int _quantiteConstatee;

    [ObservableProperty]
    private string _motif = string.Empty;

    /// <summary>
    /// Motifs proposés d'un geste. Le motif reste facultatif, mais il figure
    /// dans l'historique du stock : c'est ce qui permet, trois mois plus tard,
    /// de distinguer un vol d'une erreur de saisie. Le proposer en un tapotement
    /// donne un historique lisible sans rien imposer au vendeur.
    /// </summary>
    public IReadOnlyList<string> MotifsCourants { get; } =
    [
        "Inventaire",
        "Article abîmé",
        "Perte ou vol",
        "Erreur de saisie"
    ];

    /// <summary>Écart entre le stock enregistré et la quantité retenue.</summary>
    public int Ecart => StockApres - StockActuel;

    /// <summary>
    /// Écart mis en toutes lettres, à côté du nouveau total : « +5 »,
    /// « −3 » ou l'absence de changement.
    /// </summary>
    public string LibelleEcart => Ecart switch
    {
        0 => "aucun changement",
        > 0 => $"+{Ecart}",
        _ => $"−{-Ecart}"
    };

    /// <summary>Vrai lorsque l'opération ajoute des articles au rayon.</summary>
    public bool EcartPositif => Ecart > 0;

    /// <summary>Vrai lorsque l'opération en retire.</summary>
    public bool EcartNegatif => Ecart < 0;

    /// <summary>
    /// Motif finalement inscrit dans l'historique.
    ///
    /// Le champ est facultatif à la saisie — l'exiger faisait perdre du temps
    /// à chaque correction, pour un texte souvent tapé au hasard. Mais
    /// l'historique, lui, ne peut pas rester muet : sans motif, personne ne
    /// saurait plus tard pourquoi une quantité a changé. Une mention par
    /// défaut est donc écrite à la place.
    /// </summary>
    private string MotifRetenu => string.IsNullOrWhiteSpace(Motif)
        ? MotifParDefaut
        : Motif.Trim();

    /// <summary>Mention inscrite à défaut de motif saisi.</summary>
    private string MotifParDefaut => EstAjustement
        ? "Ajustement d'inventaire"
        : "Mouvement manuel";

    /// <summary>Rappel affiché sous le champ, pour que rien ne soit une surprise.</summary>
    public string MentionMotifParDefaut =>
        $"Laissé vide, l'historique du stock portera la mention « {MotifParDefaut} ».";

    /// <summary>
    /// Sens retenu pour le mouvement. Une entrée par défaut : c'est le geste
    /// le plus courant, et la fenêtre ne doit jamais rester sans sens valide.
    /// </summary>
    private bool EstEntree => SensChoisi?.EstEntree ?? true;

    /// <summary>Quantité qui figurera en stock après validation.</summary>
    public int StockApres => EstAjustement
        ? QuantiteConstatee
        : EstEntree
            ? StockActuel + Quantite
            : StockActuel - Quantite;

    /// <summary>Vrai lorsque l'opération ferait passer le stock sous zéro.</summary>
    public bool StockNegatif => StockApres < 0;

    public event EventHandler<bool>? FermetureDemandee;

    partial void OnQuantiteChanged(int value) => NotifierResultat();

    partial void OnQuantiteConstateeChanged(int value) => NotifierResultat();

    partial void OnSensChoisiChanged(SensMouvement? value) => NotifierResultat();

    partial void OnStockActuelChanged(int value) => NotifierResultat();

    private void NotifierResultat()
    {
        OnPropertyChanged(nameof(StockApres));
        OnPropertyChanged(nameof(StockNegatif));
        OnPropertyChanged(nameof(Ecart));
        OnPropertyChanged(nameof(LibelleEcart));
        OnPropertyChanged(nameof(EcartPositif));
        OnPropertyChanged(nameof(EcartNegatif));
    }

    /// <summary>
    /// Ajoute un article à la quantité retenue. Les deux boutons permettent
    /// de corriger un écart d'une ou deux unités — le cas le plus fréquent —
    /// sans clavier, ce qui compte sur un écran tactile posé en réserve.
    /// </summary>
    [RelayCommand]
    private void Augmenter()
    {
        if (EstAjustement)
        {
            QuantiteConstatee++;
        }
        else
        {
            Quantite++;
        }
    }

    /// <summary>Retire un article, sans jamais descendre sous zéro.</summary>
    [RelayCommand]
    private void Diminuer()
    {
        if (EstAjustement)
        {
            QuantiteConstatee = Math.Max(0, QuantiteConstatee - 1);
        }
        else
        {
            Quantite = Math.Max(1, Quantite - 1);
        }
    }

    /// <summary>Inscrit un motif proposé.</summary>
    [RelayCommand]
    private void ChoisirMotif(string? motif) => Motif = motif ?? string.Empty;

    /// <summary>Prépare la fenêtre pour l'article sélectionné.</summary>
    public void Preparer(LigneStockDto ligne, bool estAjustement)
    {
        VarianteProduitId = ligne.VarianteProduitId;
        Designation = ligne.Designation;
        Sku = ligne.Sku;
        StockActuel = ligne.QuantiteDisponible;
        QuantiteConstatee = ligne.QuantiteDisponible;
        EstAjustement = estAjustement;
        Quantite = 1;
        Motif = string.Empty;
        SensChoisi = Sens[0];
        TypeMouvement = estAjustement ? TypeMouvementStock.Ajustement : TypeMouvementStock.Correction;

        OnPropertyChanged(nameof(Titre));
        OnPropertyChanged(nameof(MentionMotifParDefaut));
        NotifierResultat();
    }

    [RelayCommand]
    private async Task ValiderAsync()
    {
        if (!EstAjustement && Quantite <= 0)
        {
            Dialogue.Avertir("La quantité doit être supérieure à zéro.", "Quantité incorrecte");
            return;
        }

        if (EstAjustement && QuantiteConstatee < 0)
        {
            Dialogue.Avertir("La quantité constatée ne peut pas être négative.", "Quantité incorrecte");
            return;
        }

        // Aucune boîte Windows par-dessus la fenêtre : celle-ci est déjà la
        // confirmation. Elle nomme l'article, montre le stock d'avant, celui
        // d'après et l'écart entre les deux, et rien ne part avant que le
        // bouton ne soit pressé. Redemander « êtes-vous sûr ? » n'ajoutait
        // aucune information — seulement un geste de plus, et l'habitude de
        // valider sans lire.

        var reussi = await ExecuterAsync(
            async () =>
            {
                if (EstAjustement)
                {
                    await _stock.AjusterStockAsync(VarianteProduitId, QuantiteConstatee, MotifRetenu)
                        .ConfigureAwait(true);
                }
                else if (EstEntree)
                {
                    await _stock.EnregistrerEntreeManuelleAsync(
                        VarianteProduitId, Quantite, TypeMouvement, MotifRetenu).ConfigureAwait(true);
                }
                else
                {
                    await _stock.EnregistrerSortieManuelleAsync(
                        VarianteProduitId, Quantite, TypeMouvement, MotifRetenu).ConfigureAwait(true);
                }
            },
            "Mouvement de stock enregistré avec succès.",
            "enregistrement d'un mouvement de stock").ConfigureAwait(true);

        if (reussi)
        {
            FermetureDemandee?.Invoke(this, true);
        }
    }

    [RelayCommand]
    private void Annuler() => FermetureDemandee?.Invoke(this, false);
}
