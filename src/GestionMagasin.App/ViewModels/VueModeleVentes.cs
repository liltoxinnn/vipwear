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

/// <summary>Historique des ventes, avec consultation du détail et impression.</summary>
public partial class VueModeleVentes : VueModeleBase
{
    private readonly IServiceVentes _ventes;
    private readonly IServiceDocumentsPdf _pdf;
    private readonly IServiceExportExcel _excel;
    private readonly ISessionUtilisateur _session;

    public VueModeleVentes(
        IServiceVentes ventes,
        IServiceDocumentsPdf pdf,
        IServiceExportExcel excel,
        ISessionUtilisateur session,
        IServiceDialogue dialogue,
        ILogger<VueModeleVentes> journal)
        : base(dialogue, journal)
    {
        _ventes = ventes;
        _pdf = pdf;
        _excel = excel;
        _session = session;

        var maintenant = DateTime.Now;
        _dateDebut = maintenant.Date.AddDays(-30);
        _dateFin = maintenant.Date;
    }

    public override string Titre => "Ventes";

    public override string SousTitre => "Historique des ventes et de leurs règlements";

    public ObservableCollection<ResumeVenteDto> Ventes { get; } = [];

    public IReadOnlyList<StatutVente> Statuts { get; } = Enum.GetValues<StatutVente>();

    [ObservableProperty]
    private ResumeVenteDto? _venteSelectionnee;

    [ObservableProperty]
    private VenteDto? _detail;

    [ObservableProperty]
    private string _recherche = string.Empty;

    [ObservableProperty]
    private DateTime? _dateDebut;

    [ObservableProperty]
    private DateTime? _dateFin;

    [ObservableProperty]
    private StatutVente? _statutFiltre;

    [ObservableProperty]
    private decimal _totalPeriode;

    [ObservableProperty]
    private int _nombreVentes;

    public bool PeutAnnuler => _session.Possede(CodesPermissions.AnnulerVente);

    public override async Task ChargerAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "chargement des ventes")
            .ConfigureAwait(true);

    private async Task ChargerListeAsync()
    {
        var periode = ConstruirePeriode();

        var resultats = await _ventes.RechercherAsync(
            periode.DebutUtc,
            periode.FinUtc,
            statut: StatutFiltre,
            recherche: Recherche).ConfigureAwait(true);

        var idPrecedent = VenteSelectionnee?.Id;

        Ventes.Clear();
        foreach (var vente in resultats)
        {
            Ventes.Add(vente);
        }

        // Les ventes annulées ne comptent pas dans le total affiché.
        TotalPeriode = resultats.Where(v => v.Statut != StatutVente.Annulee).Sum(v => v.Total);
        NombreVentes = resultats.Count(v => v.Statut != StatutVente.Annulee);

        VenteSelectionnee = idPrecedent is null
            ? Ventes.FirstOrDefault()
            : Ventes.FirstOrDefault(v => v.Id == idPrecedent) ?? Ventes.FirstOrDefault();
    }

    private Periode ConstruirePeriode() =>
        CalculateurPeriode.Construire(
            TypePeriode.Personnalisee,
            DateTime.Now,
            DateDebut ?? DateTime.Now.Date.AddYears(-5),
            DateFin ?? DateTime.Now.Date);

    partial void OnVenteSelectionneeChanged(ResumeVenteDto? value)
    {
        _ = ChargerDetailAsync(value?.Id);
    }

    private async Task ChargerDetailAsync(int? venteId)
    {
        Detail = venteId is null
            ? null
            : await _ventes.ObtenirAsync(venteId.Value).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RechercherAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "recherche de ventes").ConfigureAwait(true);

    [RelayCommand]
    private async Task ReinitialiserFiltresAsync()
    {
        Recherche = string.Empty;
        StatutFiltre = null;
        DateDebut = DateTime.Now.Date.AddDays(-30);
        DateFin = DateTime.Now.Date;

        await ExecuterAsync(ChargerListeAsync, contexteJournal: "réinitialisation des filtres")
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AfficherAujourdhuiAsync()
    {
        DateDebut = DateTime.Now.Date;
        DateFin = DateTime.Now.Date;

        await ExecuterAsync(ChargerListeAsync, contexteJournal: "filtre du jour").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AnnulerVenteAsync()
    {
        if (Detail is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une vente.", "Aucune sélection");
            return;
        }

        if (!PeutAnnuler)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation d'annuler une vente.",
                "Action non autorisée");

            return;
        }

        if (Detail.Statut == StatutVente.Annulee)
        {
            Dialogue.Avertir("Cette vente est déjà annulée.", "Vente déjà annulée");
            return;
        }

        var motif = Dialogue.DemanderTexte(
            $"Indiquez le motif de l'annulation de la vente {Detail.NumeroVente}. " +
            "Les articles seront réintégrés au stock.",
            "Annuler la vente");

        if (string.IsNullOrWhiteSpace(motif))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _ventes.AnnulerVenteAsync(Detail.Id, motif).ConfigureAwait(true);
                await ChargerListeAsync().ConfigureAwait(true);
            },
            "Vente annulée. Les articles ont été réintégrés au stock.",
            "annulation d'une vente").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task EncaisserResteAsync()
    {
        if (Detail is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une vente.", "Aucune sélection");
            return;
        }

        if (Detail.MontantRestant <= 0)
        {
            Dialogue.Informer("Cette vente est déjà réglée en totalité.", "Vente réglée");
            return;
        }

        var saisie = Dialogue.DemanderTexte(
            $"Reste à payer : {FormatageMontant.Formater(Detail.MontantRestant)}." +
            Environment.NewLine + "Montant encaissé :",
            "Encaisser le solde",
            FormatageMontant.FormaterSansDevise(Detail.MontantRestant));

        if (string.IsNullOrWhiteSpace(saisie))
        {
            return;
        }

        if (!decimal.TryParse(saisie, System.Globalization.NumberStyles.Any,
                FormatageMontant.CultureApplication, out var montant) || montant <= 0)
        {
            Dialogue.Erreur("Le montant saisi n'est pas valide.", "Montant incorrect");
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _ventes.AjouterPaiementAsync(
                    Detail.Id,
                    new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = montant })
                    .ConfigureAwait(true);

                await ChargerListeAsync().ConfigureAwait(true);
            },
            "Règlement enregistré avec succès.",
            "encaissement du solde d'une vente").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ImprimerTicketAsync() =>
        await ProduireDocumentAsync(
            v => _pdf.GenererTicketAsync(v),
            v => $"Ticket-{v.NumeroVente}.pdf",
            "impression d'un ticket").ConfigureAwait(true);

    [RelayCommand]
    private async Task ImprimerFactureAsync() =>
        await ProduireDocumentAsync(
            v => _pdf.GenererFactureAsync(v),
            v => $"Facture-{v.NumeroVente}.pdf",
            "impression d'une facture").ConfigureAwait(true);

    private async Task ProduireDocumentAsync(
        Func<VenteDto, Task<byte[]>> generateur,
        Func<VenteDto, string> nomFichier,
        string contexte)
    {
        if (Detail is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une vente.", "Aucune sélection");
            return;
        }

        await ExecuterAsync(async () =>
        {
            var contenu = await generateur(Detail).ConfigureAwait(true);

            var chemin = Dialogue.DemanderCheminEnregistrement(
                nomFichier(Detail),
                "Document PDF (*.pdf)|*.pdf");

            if (chemin is null)
            {
                return;
            }

            await File.WriteAllBytesAsync(chemin, contenu).ConfigureAwait(true);

            VueModeleCaisse.OuvrirDocument(chemin);
        }, contexteJournal: contexte).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExporterAsync() =>
        await ExecuterAsync(async () =>
        {
            var contenu = await _excel
                .ExporterVentesAsync(Ventes.ToList(), ConstruirePeriode())
                .ConfigureAwait(true);

            var chemin = Dialogue.DemanderCheminEnregistrement(
                $"Ventes-{DateTime.Now:yyyy-MM-dd}.xlsx",
                "Classeur Excel (*.xlsx)|*.xlsx");

            if (chemin is null)
            {
                return;
            }

            await File.WriteAllBytesAsync(chemin, contenu).ConfigureAwait(true);

            VueModeleCaisse.OuvrirDocument(chemin);
        }, contexteJournal: "export des ventes").ConfigureAwait(true);
}
