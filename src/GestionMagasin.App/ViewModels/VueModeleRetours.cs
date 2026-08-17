using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>Article de la vente d'origine proposé au retour.</summary>
public partial class LigneRetourSaisie : ObservableObject
{
    public required int LigneVenteId { get; init; }

    public required string Designation { get; init; }

    public required string Sku { get; init; }

    public required int QuantiteVendue { get; init; }

    public required int QuantiteDejaRetournee { get; init; }

    public required decimal PrixUnitaire { get; init; }

    /// <summary>Nombre d'exemplaires encore reprenables sur cette ligne.</summary>
    public int QuantiteRetournable => QuantiteVendue - QuantiteDejaRetournee;

    [ObservableProperty]
    private bool _selectionnee;

    [ObservableProperty]
    private int _quantiteARetourner;

    [ObservableProperty]
    private EtatArticleRetour _etatArticle = EtatArticleRetour.Revendable;

    partial void OnSelectionneeChanged(bool value)
    {
        // Cocher une ligne propose d'emblée de reprendre tout ce qui reste.
        if (value && QuantiteARetourner == 0)
        {
            QuantiteARetourner = QuantiteRetournable;
        }
        else if (!value)
        {
            QuantiteARetourner = 0;
        }

        LigneModifiee?.Invoke(this, EventArgs.Empty);
    }

    partial void OnQuantiteARetournerChanged(int value) =>
        LigneModifiee?.Invoke(this, EventArgs.Empty);

    partial void OnEtatArticleChanged(EtatArticleRetour value) =>
        LigneModifiee?.Invoke(this, EventArgs.Empty);

    public event EventHandler? LigneModifiee;
}

/// <summary>
/// Écran des retours : recherche de la vente d'origine, sélection des
/// articles rapportés, choix de leur état, puis validation.
/// </summary>
public partial class VueModeleRetours : VueModeleBase
{
    private readonly IServiceRetours _retours;
    private readonly IServiceVentes _ventes;
    private readonly IServiceDocumentsPdf _pdf;
    private readonly IServiceExportExcel _excel;

    public VueModeleRetours(
        IServiceRetours retours,
        IServiceVentes ventes,
        IServiceDocumentsPdf pdf,
        IServiceExportExcel excel,
        IServiceDialogue dialogue,
        ILogger<VueModeleRetours> journal)
        : base(dialogue, journal)
    {
        _retours = retours;
        _ventes = ventes;
        _pdf = pdf;
        _excel = excel;

        _dateDebut = DateTime.Now.Date.AddMonths(-3);
        _dateFin = DateTime.Now.Date;
    }

    public override string Titre => "Retours";

    public override string SousTitre => "Reprise d'articles et remboursement des clients";

    public ObservableCollection<ResumeRetourDto> Retours { get; } = [];

    public ObservableCollection<LigneRetourSaisie> ArticlesRetournables { get; } = [];

    public IReadOnlyList<EtatArticleRetour> EtatsArticle { get; } = Enum.GetValues<EtatArticleRetour>();

    [ObservableProperty]
    private ResumeRetourDto? _retourSelectionne;

    [ObservableProperty]
    private RetourDto? _detail;

    // --- Recherche de la vente à retourner ---

    [ObservableProperty]
    private string _numeroVenteRecherche = string.Empty;

    [ObservableProperty]
    private VenteDto? _venteOrigine;

    [ObservableProperty]
    private string _motifRetour = string.Empty;

    [ObservableProperty]
    private decimal _montantARembourser;

    // --- Filtres de la liste ---

    [ObservableProperty]
    private string _recherche = string.Empty;

    [ObservableProperty]
    private DateTime? _dateDebut;

    [ObservableProperty]
    private DateTime? _dateFin;

    [ObservableProperty]
    private decimal _totalRembourse;

    public override async Task ChargerAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "chargement des retours")
            .ConfigureAwait(true);

    private async Task ChargerListeAsync()
    {
        var periode = ConstruirePeriode();

        var resultats = await _retours.RechercherAsync(
            periode.DebutUtc,
            periode.FinUtc,
            recherche: Recherche).ConfigureAwait(true);

        var idPrecedent = RetourSelectionne?.Id;

        Retours.Clear();
        foreach (var retour in resultats)
        {
            Retours.Add(retour);
        }

        TotalRembourse = resultats.Sum(r => r.TotalRembourse);

        RetourSelectionne = idPrecedent is null
            ? Retours.FirstOrDefault()
            : Retours.FirstOrDefault(r => r.Id == idPrecedent) ?? Retours.FirstOrDefault();
    }

    private Periode ConstruirePeriode() =>
        CalculateurPeriode.Construire(
            TypePeriode.Personnalisee,
            DateTime.Now,
            DateDebut ?? DateTime.Now.Date.AddYears(-5),
            DateFin ?? DateTime.Now.Date);

    partial void OnRetourSelectionneChanged(ResumeRetourDto? value)
    {
        _ = ChargerDetailAsync(value?.Id);
    }

    private async Task ChargerDetailAsync(int? retourId)
    {
        Detail = retourId is null
            ? null
            : await _retours.ObtenirAsync(retourId.Value).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RechercherAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "recherche de retours")
            .ConfigureAwait(true);

    // ==================================================================
    // Saisie d'un nouveau retour
    // ==================================================================

    [RelayCommand]
    private async Task ChercherVenteAsync()
    {
        var numero = NumeroVenteRecherche.Trim();

        if (string.IsNullOrEmpty(numero))
        {
            Dialogue.Avertir(
                "Saisissez le numéro de la vente figurant sur le ticket du client.",
                "Numéro manquant");

            return;
        }

        await ExecuterAsync(async () =>
        {
            var vente = await _ventes.ObtenirParNumeroAsync(numero).ConfigureAwait(true);

            if (vente is null)
            {
                VenteOrigine = null;
                ArticlesRetournables.Clear();

                Dialogue.Avertir(
                    $"Aucune vente ne porte le numéro « {numero} ». " +
                    "Vérifiez le ticket du client.",
                    "Vente introuvable");

                return;
            }

            if (vente.Statut == StatutVente.Annulee)
            {
                VenteOrigine = null;
                ArticlesRetournables.Clear();

                Dialogue.Avertir(
                    "Cette vente a été annulée : ses articles ont déjà été réintégrés au stock.",
                    "Vente annulée");

                return;
            }

            if (!vente.PeutEtreRetournee)
            {
                VenteOrigine = null;
                ArticlesRetournables.Clear();

                Dialogue.Informer(
                    "Tous les articles de cette vente ont déjà été retournés.",
                    "Retour déjà effectué");

                return;
            }

            VenteOrigine = vente;
            PreparerLignes(vente);
        }, contexteJournal: "recherche d'une vente à retourner").ConfigureAwait(true);
    }

    private void PreparerLignes(VenteDto vente)
    {
        ArticlesRetournables.Clear();

        foreach (var ligne in vente.Lignes.Where(l => l.QuantiteRetournable > 0))
        {
            var saisie = new LigneRetourSaisie
            {
                LigneVenteId = ligne.Id,
                Designation = ligne.Designation,
                Sku = ligne.Sku,
                QuantiteVendue = ligne.Quantite,
                QuantiteDejaRetournee = ligne.QuantiteRetournee,
                PrixUnitaire = ligne.PrixUnitaire
            };

            saisie.LigneModifiee += (_, _) => RecalculerRemboursement();

            ArticlesRetournables.Add(saisie);
        }

        RecalculerRemboursement();
    }

    private void RecalculerRemboursement()
    {
        if (VenteOrigine is null)
        {
            MontantARembourser = 0m;
            return;
        }

        // Le remboursement estimé tient compte de la remise du ticket, comme
        // le fera le calcul définitif côté service.
        var facteur = VenteOrigine.SousTotal > 0
            ? VenteOrigine.Total / VenteOrigine.SousTotal
            : 1m;

        decimal total = 0m;

        foreach (var ligne in ArticlesRetournables.Where(l => l.Selectionnee && l.QuantiteARetourner > 0))
        {
            var ligneVente = VenteOrigine.Lignes.First(l => l.Id == ligne.LigneVenteId);
            var unitaire = ligneVente.Quantite > 0
                ? (ligneVente.Total / ligneVente.Quantite) * facteur
                : 0m;

            total += Math.Round(unitaire * ligne.QuantiteARetourner, 2);
        }

        MontantARembourser = total;
    }

    [RelayCommand]
    private async Task ValiderRetourAsync()
    {
        if (VenteOrigine is null)
        {
            Dialogue.Avertir("Recherchez d'abord la vente d'origine.", "Aucune vente sélectionnée");
            return;
        }

        var lignes = ArticlesRetournables
            .Where(l => l.Selectionnee && l.QuantiteARetourner > 0)
            .ToList();

        if (lignes.Count == 0)
        {
            Dialogue.Avertir(
                "Sélectionnez au moins un article à reprendre et indiquez sa quantité.",
                "Aucun article sélectionné");

            return;
        }

        var enExces = lignes.FirstOrDefault(l => l.QuantiteARetourner > l.QuantiteRetournable);

        if (enExces is not null)
        {
            Dialogue.Erreur(
                $"La quantité saisie pour « {enExces.Designation} » dépasse ce qui peut encore " +
                $"être repris ({enExces.QuantiteRetournable} exemplaire(s)).",
                "Quantité incorrecte");

            return;
        }

        var endommages = lignes
            .Where(l => l.EtatArticle == EtatArticleRetour.Endommage)
            .Sum(l => l.QuantiteARetourner);

        var message =
            $"Enregistrer le retour de {lignes.Sum(l => l.QuantiteARetourner)} article(s) " +
            $"pour un remboursement de {FormatageMontant.Formater(MontantARembourser)} ?";

        if (endommages > 0)
        {
            message += Environment.NewLine + Environment.NewLine +
                       $"{endommages} article(s) déclaré(s) endommagé(s) ne seront pas remis en stock.";
        }

        if (!Dialogue.Confirmer(message, "Valider le retour"))
        {
            return;
        }

        var demande = new DemandeRetour
        {
            VenteId = VenteOrigine.Id,
            Motif = string.IsNullOrWhiteSpace(MotifRetour) ? null : MotifRetour.Trim(),
            Lignes = lignes.Select(l => new LigneRetourDemande
            {
                LigneVenteId = l.LigneVenteId,
                Quantite = l.QuantiteARetourner,
                EtatArticle = l.EtatArticle
            }).ToList()
        };

        var retour = await ExecuterAsync(
            () => _retours.EnregistrerRetourAsync(demande),
            contexteJournal: "validation d'un retour").ConfigureAwait(true);

        if (retour is null)
        {
            return;
        }

        VenteOrigine = null;
        ArticlesRetournables.Clear();
        NumeroVenteRecherche = string.Empty;
        MotifRetour = string.Empty;
        MontantARembourser = 0m;

        await ChargerListeAsync().ConfigureAwait(true);

        RetourSelectionne = Retours.FirstOrDefault(r => r.Id == retour.Id) ?? RetourSelectionne;

        if (Dialogue.Confirmer(
                $"Retour {retour.NumeroRetour} enregistré. " +
                $"Montant remboursé : {FormatageMontant.Formater(retour.TotalRembourse)}." +
                Environment.NewLine + Environment.NewLine +
                "Voulez-vous imprimer le bon de retour ?",
                "Retour enregistré"))
        {
            await ImprimerBonAsync(retour).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void AnnulerSaisie()
    {
        VenteOrigine = null;
        ArticlesRetournables.Clear();
        NumeroVenteRecherche = string.Empty;
        MotifRetour = string.Empty;
        MontantARembourser = 0m;
    }

    [RelayCommand]
    private async Task ImprimerBonSelectionneAsync()
    {
        if (Detail is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un retour.", "Aucune sélection");
            return;
        }

        await ImprimerBonAsync(Detail).ConfigureAwait(true);
    }

    private async Task ImprimerBonAsync(RetourDto retour) =>
        await ExecuterAsync(async () =>
        {
            var contenu = await _pdf.GenererBonRetourAsync(retour).ConfigureAwait(true);

            var chemin = Dialogue.DemanderCheminEnregistrement(
                $"Bon-retour-{retour.NumeroRetour}.pdf",
                "Document PDF (*.pdf)|*.pdf");

            if (chemin is null)
            {
                return;
            }

            await File.WriteAllBytesAsync(chemin, contenu).ConfigureAwait(true);

            VueModeleCaisse.OuvrirDocument(chemin);
        }, contexteJournal: "impression d'un bon de retour").ConfigureAwait(true);

    [RelayCommand]
    private async Task ExporterAsync() =>
        await ExecuterAsync(async () =>
        {
            var contenu = await _excel
                .ExporterRetoursAsync(Retours.ToList(), ConstruirePeriode())
                .ConfigureAwait(true);

            var chemin = Dialogue.DemanderCheminEnregistrement(
                $"Retours-{DateTime.Now:yyyy-MM-dd}.xlsx",
                "Classeur Excel (*.xlsx)|*.xlsx");

            if (chemin is null)
            {
                return;
            }

            await File.WriteAllBytesAsync(chemin, contenu).ConfigureAwait(true);

            VueModeleCaisse.OuvrirDocument(chemin);
        }, contexteJournal: "export des retours").ConfigureAwait(true);
}
