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
    }

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
        NotifierResultat();
    }

    [RelayCommand]
    private async Task ValiderAsync()
    {
        if (string.IsNullOrWhiteSpace(Motif))
        {
            Dialogue.Avertir(
                "Veuillez indiquer le motif de ce mouvement : il figurera dans l'historique.",
                "Motif obligatoire");

            return;
        }

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

        var message = EstAjustement
            ? $"Ajuster le stock de « {Designation} » de {StockActuel} à {QuantiteConstatee} ?"
            : $"Enregistrer {(EstEntree ? "une entrée" : "une sortie")} de {Quantite} " +
              $"article(s) pour « {Designation} » ?" + Environment.NewLine +
              $"Le stock passera de {StockActuel} à {StockApres}.";

        if (!Dialogue.Confirmer(message, "Confirmer le mouvement"))
        {
            return;
        }

        var reussi = await ExecuterAsync(
            async () =>
            {
                if (EstAjustement)
                {
                    await _stock.AjusterStockAsync(VarianteProduitId, QuantiteConstatee, Motif)
                        .ConfigureAwait(true);
                }
                else if (EstEntree)
                {
                    await _stock.EnregistrerEntreeManuelleAsync(
                        VarianteProduitId, Quantite, TypeMouvement, Motif).ConfigureAwait(true);
                }
                else
                {
                    await _stock.EnregistrerSortieManuelleAsync(
                        VarianteProduitId, Quantite, TypeMouvement, Motif).ConfigureAwait(true);
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
