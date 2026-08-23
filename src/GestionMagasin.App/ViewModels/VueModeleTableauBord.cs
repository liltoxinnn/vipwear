using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>
/// Tableau de bord : indicateurs du jour et du mois, courbes d'évolution,
/// meilleures ventes, alertes de stock et dernières ventes.
/// </summary>
public partial class VueModeleTableauBord : VueModeleBase
{
    private readonly IServiceRapports _rapports;

    public VueModeleTableauBord(
        IServiceRapports rapports,
        IServiceDialogue dialogue,
        ILogger<VueModeleTableauBord> journal)
        : base(dialogue, journal)
    {
        _rapports = rapports;
    }

    public override string Titre => "Tableau de bord";

    public override string SousTitre => "Activité du magasin en un coup d'œil";

    [ObservableProperty]
    private SyntheseActiviteDto _jour = new();

    [ObservableProperty]
    private SyntheseActiviteDto _mois = new();

    [ObservableProperty]
    private SyntheseStockDto _stock = new();

    /// <summary>Plus haute valeur des courbes : sert d'échelle aux barres.</summary>
    [ObservableProperty]
    private decimal _maximumVentesJournalieres = 1m;

    [ObservableProperty]
    private decimal _maximumVentesMensuelles = 1m;

    public ObservableCollection<PointStatistique> EvolutionVentes { get; } = [];

    public ObservableCollection<PointStatistique> EvolutionMensuelle { get; } = [];

    public ObservableCollection<ClassementArticleDto> MeilleuresVentes { get; } = [];

    public ObservableCollection<ResumeVenteDto> DernieresVentes { get; } = [];

    public ObservableCollection<LigneStockDto> StockFaible { get; } = [];

    public ObservableCollection<LigneStockDto> Ruptures { get; } = [];


    public override async Task ChargerAsync() =>
        await ExecuterAsync(async () =>
        {
            var tableau = await _rapports.ObtenirTableauBordAsync().ConfigureAwait(true);

            Jour = tableau.Jour;
            Mois = tableau.Mois;
            Stock = tableau.Stock;

            Remplir(EvolutionVentes, tableau.EvolutionVentes);
            Remplir(EvolutionMensuelle, tableau.EvolutionMensuelle);
            Remplir(MeilleuresVentes, tableau.MeilleuresVentes);
            Remplir(DernieresVentes, tableau.DernieresVentes);
            Remplir(StockFaible, tableau.StockFaible);
            Remplir(Ruptures, tableau.Ruptures);

            // Une échelle strictement positive évite une division par zéro
            // lorsque aucune vente n'a encore été enregistrée.
            MaximumVentesJournalieres = Maximum(tableau.EvolutionVentes);
            MaximumVentesMensuelles = Maximum(tableau.EvolutionMensuelle);
        }, contexteJournal: "chargement du tableau de bord").ConfigureAwait(true);

    [RelayCommand]
    private async Task ActualiserAsync() => await ChargerAsync().ConfigureAwait(true);

    private static decimal Maximum(IReadOnlyList<PointStatistique> points)
    {
        var maximum = points.Count == 0 ? 0m : points.Max(p => p.Valeur);

        return maximum <= 0 ? 1m : maximum;
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
