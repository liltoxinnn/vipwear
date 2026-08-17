using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>Choix de période proposé dans les filtres de rapports.</summary>
public record ChoixPeriode(TypePeriode Type, string Libelle);

/// <summary>
/// Rapports : chiffre d'affaires, bénéfice, ventes, achats, retours, stock,
/// classements d'articles, ventes par employé et par mode de paiement.
/// Toutes les valeurs proviennent des écritures réelles.
/// </summary>
public partial class VueModeleRapports : VueModeleBase
{
    private readonly IServiceRapports _rapports;
    private readonly IServiceDocumentsPdf _pdf;
    private readonly IServiceExportExcel _excel;

    public VueModeleRapports(
        IServiceRapports rapports,
        IServiceDocumentsPdf pdf,
        IServiceExportExcel excel,
        IServiceDialogue dialogue,
        ILogger<VueModeleRapports> journal)
        : base(dialogue, journal)
    {
        _rapports = rapports;
        _pdf = pdf;
        _excel = excel;

        _dateDebut = DateTime.Now.Date.AddDays(-30);
        _dateFin = DateTime.Now.Date;
        _periodeChoisie = Periodes[2];
    }

    public override string Titre => "Rapports";

    public override string SousTitre => "Chiffre d'affaires, bénéfices et classements";

    public IReadOnlyList<ChoixPeriode> Periodes { get; } =
    [
        new(TypePeriode.Jour, "Aujourd'hui"),
        new(TypePeriode.Semaine, "Cette semaine"),
        new(TypePeriode.Mois, "Ce mois-ci"),
        new(TypePeriode.Annee, "Cette année"),
        new(TypePeriode.Personnalisee, "Période personnalisée")
    ];

    // Nullable à dessein : lorsque l'écran est quitté, WPF vide la sélection
    // de la liste déroulante avant de détruire la vue et réécrit « rien » dans
    // cette propriété. Le type doit accepter cette valeur, sans quoi tout ce
    // qui lit la période plante au moment de changer d'écran.
    [ObservableProperty]
    private ChoixPeriode? _periodeChoisie;

    [ObservableProperty]
    private DateTime? _dateDebut;

    [ObservableProperty]
    private DateTime? _dateFin;

    [ObservableProperty]
    private string _libellePeriode = string.Empty;

    /// <summary>Vrai lorsque les champs de dates doivent être proposés.</summary>
    public bool PeriodePersonnalisee => PeriodeChoisie?.Type == TypePeriode.Personnalisee;

    /// <summary>Période effectivement retenue, « Ce mois-ci » à défaut de choix.</summary>
    private TypePeriode TypeRetenu => PeriodeChoisie?.Type ?? TypePeriode.Mois;

    // --- Résultats ---

    [ObservableProperty]
    private SyntheseActiviteDto _synthese = new();

    [ObservableProperty]
    private SyntheseAchatsDto _syntheseAchats = new();

    [ObservableProperty]
    private SyntheseRetoursDto _syntheseRetours = new();

    [ObservableProperty]
    private SyntheseStockDto _syntheseStock = new();

    [ObservableProperty]
    private decimal _maximumEvolution = 1m;

    public ObservableCollection<PointStatistique> Evolution { get; } = [];

    public ObservableCollection<ClassementArticleDto> MeilleuresVentes { get; } = [];

    public ObservableCollection<ClassementArticleDto> MoinsBonnesVentes { get; } = [];

    public ObservableCollection<VentesParEmployeDto> VentesParEmploye { get; } = [];

    public ObservableCollection<VentesParModePaiementDto> VentesParModePaiement { get; } = [];

    partial void OnPeriodeChoisieChanged(ChoixPeriode? value)
    {
        OnPropertyChanged(nameof(PeriodePersonnalisee));

        // Sélection vidée par la fermeture de l'écran, et non par l'utilisateur :
        // il n'y a aucun rapport à recalculer.
        if (value is null)
        {
            return;
        }

        _ = ChargerAsync();
    }

    public override async Task ChargerAsync() =>
        await ExecuterAsync(async () =>
        {
            var periode = ConstruirePeriode();

            LibellePeriode = periode.Libelle;

            Synthese = await _rapports.ObtenirSyntheseAsync(periode).ConfigureAwait(true);
            SyntheseAchats = await _rapports.ObtenirSyntheseAchatsAsync(periode).ConfigureAwait(true);
            SyntheseRetours = await _rapports.ObtenirSyntheseRetoursAsync(periode).ConfigureAwait(true);
            SyntheseStock = await _rapports.ObtenirSyntheseStockAsync().ConfigureAwait(true);

            // Une période longue est présentée par mois, une période courte
            // par jour : la courbe reste lisible dans les deux cas.
            var dureeJours = (periode.FinUtc - periode.DebutUtc).TotalDays;

            var points = dureeJours > 92
                ? await _rapports.ObtenirVentesParMoisAsync(periode).ConfigureAwait(true)
                : await _rapports.ObtenirVentesParJourAsync(periode).ConfigureAwait(true);

            Remplir(Evolution, points);
            MaximumEvolution = points.Count == 0 || points.Max(p => p.Valeur) <= 0
                ? 1m
                : points.Max(p => p.Valeur);

            Remplir(MeilleuresVentes,
                await _rapports.ObtenirMeilleuresVentesAsync(periode, 20).ConfigureAwait(true));

            Remplir(MoinsBonnesVentes,
                await _rapports.ObtenirMoinsBonnesVentesAsync(periode, 20).ConfigureAwait(true));

            Remplir(VentesParEmploye,
                await _rapports.ObtenirVentesParEmployeAsync(periode).ConfigureAwait(true));

            Remplir(VentesParModePaiement,
                await _rapports.ObtenirVentesParModePaiementAsync(periode).ConfigureAwait(true));
        }, contexteJournal: "calcul des rapports").ConfigureAwait(true);

    private Periode ConstruirePeriode() =>
        CalculateurPeriode.Construire(
            TypeRetenu,
            DateTime.Now,
            DateDebut,
            DateFin);

    [RelayCommand]
    private async Task AppliquerAsync() => await ChargerAsync().ConfigureAwait(true);

    [RelayCommand]
    private async Task ImprimerAsync() =>
        await ExecuterAsync(async () =>
        {
            var periode = ConstruirePeriode();

            var contenu = await _pdf.GenererRapportActiviteAsync(
                periode,
                Synthese,
                MeilleuresVentes.ToList(),
                VentesParEmploye.ToList(),
                VentesParModePaiement.ToList()).ConfigureAwait(true);

            await EnregistrerAsync(
                contenu,
                $"Rapport-activite-{DateTime.Now:yyyy-MM-dd}.pdf",
                "Document PDF (*.pdf)|*.pdf").ConfigureAwait(true);
        }, contexteJournal: "impression du rapport d'activité").ConfigureAwait(true);

    [RelayCommand]
    private async Task ExporterAsync() =>
        await ExecuterAsync(async () =>
        {
            var periode = ConstruirePeriode();

            var contenu = await _excel.ExporterRapportActiviteAsync(
                periode,
                Synthese,
                MeilleuresVentes.ToList(),
                MoinsBonnesVentes.ToList(),
                VentesParEmploye.ToList(),
                VentesParModePaiement.ToList()).ConfigureAwait(true);

            await EnregistrerAsync(
                contenu,
                $"Rapport-activite-{DateTime.Now:yyyy-MM-dd}.xlsx",
                "Classeur Excel (*.xlsx)|*.xlsx").ConfigureAwait(true);
        }, contexteJournal: "export du rapport d'activité").ConfigureAwait(true);

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

    private static void Remplir<T>(ObservableCollection<T> destination, IReadOnlyList<T> source)
    {
        destination.Clear();

        foreach (var element in source)
        {
            destination.Add(element);
        }
    }
}
