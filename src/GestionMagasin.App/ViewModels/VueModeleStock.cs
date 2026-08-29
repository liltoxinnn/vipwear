using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Securite;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>
/// Écran de stock : état des quantités, alertes, mouvements manuels et
/// historique. Toutes les écritures passent par le service de stock.
/// </summary>
public partial class VueModeleStock : VueModeleBase
{
    private readonly IServiceStock _stock;
    private readonly IServiceRapports _rapports;
    private readonly IServiceDocumentsPdf _pdf;
    private readonly IServiceExportExcel _excel;
    private readonly ISessionUtilisateur _session;

    public VueModeleStock(
        IServiceStock stock,
        IServiceRapports rapports,
        IServiceDocumentsPdf pdf,
        IServiceExportExcel excel,
        ISessionUtilisateur session,
        IServiceDialogue dialogue,
        ILogger<VueModeleStock> journal)
        : base(dialogue, journal)
    {
        _stock = stock;
        _rapports = rapports;
        _pdf = pdf;
        _excel = excel;
        _session = session;
    }

    public override string Titre => "Stock";

    public override string SousTitre => "Quantités disponibles, alertes et historique des mouvements";

    /// <summary>
    /// Déclinaisons répondant aux filtres. Elles alimentent le regroupement
    /// par produit, et ne sont affichées telles quelles qu'une fois un
    /// produit ouvert.
    /// </summary>
    public ObservableCollection<LigneStockDto> Lignes { get; } = [];

    /// <summary>
    /// Produits, avec leur stock cumulé. C'est la liste affichée par défaut :
    /// un catalogue de deux cents produits en quatre couleurs et six tailles
    /// ferait cinq mille lignes, où personne ne lirait rien.
    /// </summary>
    public ObservableCollection<LigneStockProduit> Produits { get; } = [];

    /// <summary>Déclinaisons du produit ouvert, dans l'ordre des tailles.</summary>
    public ObservableCollection<LigneStockDto> Declinaisons { get; } = [];

    public ObservableCollection<MouvementStockDto> Historique { get; } = [];

    public IReadOnlyList<TypeMouvementStock> TypesMouvement { get; } =
        Enum.GetValues<TypeMouvementStock>();

    [ObservableProperty]
    private LigneStockDto? _ligneSelectionnee;

    /// <summary>
    /// Produit dont on consulte le détail. Tant qu'il est vide, l'écran
    /// montre la liste des produits.
    /// </summary>
    [ObservableProperty]
    private LigneStockProduit? _produitOuvert;

    [ObservableProperty]
    private LigneStockProduit? _produitSelectionne;

    [ObservableProperty]
    private string _recherche = string.Empty;

    [ObservableProperty]
    private bool _uniquementStockFaible;

    [ObservableProperty]
    private bool _uniquementRuptures;

    [ObservableProperty]
    private bool _inclureInactifs;

    [ObservableProperty]
    private SyntheseStockDto _synthese = new();

    public bool PeutModifierStock => _session.Possede(CodesPermissions.ModifierStock);

    /// <summary>Demande l'ouverture de la fenêtre de mouvement de stock.</summary>
    public event EventHandler<(LigneStockDto Ligne, bool EstAjustement)>? MouvementDemande;

    public override async Task ChargerAsync() =>
        await ExecuterAsync(async () =>
        {
            await ChargerListeAsync().ConfigureAwait(true);

            if (_session.Possede(CodesPermissions.VoirRapports))
            {
                Synthese = await _rapports.ObtenirSyntheseStockAsync().ConfigureAwait(true);
            }
        }, contexteJournal: "chargement du stock").ConfigureAwait(true);

    private async Task ChargerListeAsync()
    {
        var resultats = await _stock.ListerStockAsync(
            Recherche,
            UniquementStockFaible,
            UniquementRuptures,
            InclureInactifs).ConfigureAwait(true);

        var idPrecedent = LigneSelectionnee?.VarianteProduitId;
        var produitPrecedent = ProduitOuvert?.ProduitId;

        Lignes.Clear();
        foreach (var ligne in resultats)
        {
            Lignes.Add(ligne);
        }

        Produits.Clear();
        foreach (var produit in LigneStockProduit.Regrouper(resultats))
        {
            Produits.Add(produit);
        }

        // Le produit ouvert le reste après un rechargement — sauf s'il sort
        // du filtre, auquel cas le laisser ouvert sur une liste vide n'aurait
        // aucun sens.
        ProduitOuvert = produitPrecedent is null
            ? null
            : Produits.FirstOrDefault(p => p.ProduitId == produitPrecedent);

        RemplirDeclinaisons(idPrecedent);
    }

    /// <summary>
    /// Recharge le détail du produit ouvert et rétablit la sélection, pour
    /// que l'historique affiché à droite ne saute pas d'un article à l'autre
    /// après une correction de stock.
    /// </summary>
    private void RemplirDeclinaisons(int? varianteARetrouver)
    {
        Declinaisons.Clear();

        if (ProduitOuvert is not null)
        {
            var detail = Lignes
                .Where(l => l.ProduitId == ProduitOuvert.ProduitId)
                .OrderBy(l => l.Couleur)
                .ThenBy(l => l.OrdreTaille)
                .ThenBy(l => l.Taille);

            foreach (var ligne in detail)
            {
                Declinaisons.Add(ligne);
            }
        }

        LigneSelectionnee = varianteARetrouver is null
            ? Declinaisons.FirstOrDefault()
            : Declinaisons.FirstOrDefault(l => l.VarianteProduitId == varianteARetrouver)
              ?? Declinaisons.FirstOrDefault();
    }

    /// <summary>Ouvre le détail d'un produit : ses couleurs et ses tailles.</summary>
    [RelayCommand]
    private void OuvrirProduit(LigneStockProduit? produit)
    {
        if (produit is null)
        {
            return;
        }

        ProduitOuvert = produit;

        RemplirDeclinaisons(null);
    }

    /// <summary>Referme le détail et réaffiche la liste des produits.</summary>
    [RelayCommand]
    private void FermerProduit()
    {
        ProduitOuvert = null;
        ProduitSelectionne = null;

        RemplirDeclinaisons(null);
    }

    partial void OnLigneSelectionneeChanged(LigneStockDto? value)
    {
        _ = ExecuterAsync(() => ChargerHistoriqueAsync(value?.VarianteProduitId),
            contexteJournal: "chargement de l'historique de stock");
    }

    private async Task ChargerHistoriqueAsync(int? varianteId)
    {
        Historique.Clear();

        if (varianteId is null)
        {
            return;
        }

        var mouvements = await _stock.ObtenirHistoriqueAsync(varianteId, limite: 200).ConfigureAwait(true);

        foreach (var mouvement in mouvements)
        {
            Historique.Add(mouvement);
        }
    }

    [RelayCommand]
    private async Task RechercherAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "recherche dans le stock")
            .ConfigureAwait(true);

    [RelayCommand]
    private async Task ReinitialiserFiltresAsync()
    {
        Recherche = string.Empty;
        UniquementStockFaible = false;
        UniquementRuptures = false;
        InclureInactifs = false;

        await ExecuterAsync(ChargerListeAsync, contexteJournal: "réinitialisation des filtres")
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AfficherStockFaibleAsync()
    {
        UniquementRuptures = false;
        UniquementStockFaible = true;

        await ExecuterAsync(ChargerListeAsync, contexteJournal: "filtre stock faible")
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AfficherRupturesAsync()
    {
        UniquementStockFaible = false;
        UniquementRuptures = true;

        await ExecuterAsync(ChargerListeAsync, contexteJournal: "filtre ruptures").ConfigureAwait(true);
    }

    [RelayCommand]
    private void NouveauMouvement()
    {
        if (!VerifierSelectionEtDroits())
        {
            return;
        }

        MouvementDemande?.Invoke(this, (LigneSelectionnee!, false));
    }

    [RelayCommand]
    private void AjusterStock()
    {
        if (!VerifierSelectionEtDroits())
        {
            return;
        }

        MouvementDemande?.Invoke(this, (LigneSelectionnee!, true));
    }

    /// <summary>
    /// Corrige la quantité d'une déclinaison désignée directement.
    ///
    /// C'est le geste attendu à l'inventaire : on lit une quantité qui ne
    /// correspond pas au rayon, on la touche, on la corrige. Passer par une
    /// sélection puis un bouton de la barre d'outils faisait trois gestes là
    /// où il en faut un, et laissait la place à l'erreur la plus coûteuse —
    /// corriger la quantité d'une autre taille que celle qu'on regarde.
    /// </summary>
    [RelayCommand]
    private void AjusterLigne(LigneStockDto? ligne)
    {
        if (ligne is null)
        {
            return;
        }

        // La ligne touchée devient la ligne courante : l'historique affiché à
        // droite suit, et la fenêtre de correction porte bien sur elle.
        LigneSelectionnee = ligne;

        if (!VerifierSelectionEtDroits())
        {
            return;
        }

        MouvementDemande?.Invoke(this, (ligne, true));
    }

    private bool VerifierSelectionEtDroits()
    {
        if (LigneSelectionnee is null)
        {
            Dialogue.Avertir(
                ProduitOuvert is null
                    ? "Ouvrez d'abord un produit, puis choisissez la taille et la couleur à corriger."
                    : "Sélectionnez d'abord une taille dans la liste.",
                "Aucune sélection");

            return false;
        }

        if (!PeutModifierStock)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation de modifier le stock.",
                "Action non autorisée");

            return false;
        }

        return true;
    }

    /// <summary>Recharge la liste et l'historique après un mouvement.</summary>
    public async Task RechargerAsync()
    {
        await ExecuterAsync(async () =>
        {
            await ChargerListeAsync().ConfigureAwait(true);
            await ChargerHistoriqueAsync(LigneSelectionnee?.VarianteProduitId).ConfigureAwait(true);

            if (_session.Possede(CodesPermissions.VoirRapports))
            {
                Synthese = await _rapports.ObtenirSyntheseStockAsync().ConfigureAwait(true);
            }
        }, contexteJournal: "rechargement du stock").ConfigureAwait(true);
    }

    // ==================================================================
    // Exports
    // ==================================================================

    [RelayCommand]
    private async Task ExporterExcelAsync() =>
        await ExecuterAsync(async () =>
        {
            var contenu = await _excel.ExporterStockAsync(Lignes.ToList()).ConfigureAwait(true);

            await EnregistrerAsync(
                contenu,
                $"Stock-{DateTime.Now:yyyy-MM-dd}.xlsx",
                "Classeur Excel (*.xlsx)|*.xlsx").ConfigureAwait(true);
        }, contexteJournal: "export Excel du stock").ConfigureAwait(true);

    [RelayCommand]
    private async Task ImprimerAsync() =>
        await ExecuterAsync(async () =>
        {
            var synthese = _session.Possede(CodesPermissions.VoirRapports)
                ? await _rapports.ObtenirSyntheseStockAsync().ConfigureAwait(true)
                : new SyntheseStockDto();

            var contenu = await _pdf.GenererRapportStockAsync(Lignes.ToList(), synthese)
                .ConfigureAwait(true);

            await EnregistrerAsync(
                contenu,
                $"Etat-du-stock-{DateTime.Now:yyyy-MM-dd}.pdf",
                "Document PDF (*.pdf)|*.pdf").ConfigureAwait(true);
        }, contexteJournal: "impression de l'état du stock").ConfigureAwait(true);

    [RelayCommand]
    private async Task ExporterHistoriqueAsync() =>
        await ExecuterAsync(async () =>
        {
            var mouvements = await _stock.ObtenirHistoriqueAsync(limite: 5000).ConfigureAwait(true);

            var contenu = await _excel.ExporterMouvementsStockAsync(mouvements).ConfigureAwait(true);

            await EnregistrerAsync(
                contenu,
                $"Mouvements-stock-{DateTime.Now:yyyy-MM-dd}.xlsx",
                "Classeur Excel (*.xlsx)|*.xlsx").ConfigureAwait(true);
        }, contexteJournal: "export de l'historique de stock").ConfigureAwait(true);

    private async Task EnregistrerAsync(byte[] contenu, string nomParDefaut, string filtre)
    {
        var chemin = Dialogue.DemanderCheminEnregistrement(nomParDefaut, filtre);

        if (chemin is null)
        {
            return;
        }

        await File.WriteAllBytesAsync(chemin, contenu).ConfigureAwait(true);

        VueModeleCaisse.OuvrirDocument(chemin);
    }
}
