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

    public ObservableCollection<LigneStockDto> Lignes { get; } = [];

    public ObservableCollection<MouvementStockDto> Historique { get; } = [];

    public IReadOnlyList<TypeMouvementStock> TypesMouvement { get; } =
        Enum.GetValues<TypeMouvementStock>();

    [ObservableProperty]
    private LigneStockDto? _ligneSelectionnee;

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

        Lignes.Clear();
        foreach (var ligne in resultats)
        {
            Lignes.Add(ligne);
        }

        LigneSelectionnee = idPrecedent is null
            ? Lignes.FirstOrDefault()
            : Lignes.FirstOrDefault(l => l.VarianteProduitId == idPrecedent) ?? Lignes.FirstOrDefault();
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

    private bool VerifierSelectionEtDroits()
    {
        if (LigneSelectionnee is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un article dans la liste.", "Aucune sélection");
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
