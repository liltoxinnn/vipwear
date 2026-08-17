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
/// Commandes fournisseurs : consultation, réception de la marchandise et
/// règlements. La réception est le seul geste qui alimente le stock.
/// </summary>
public partial class VueModeleAchats : VueModeleBase
{
    private readonly IServiceAchats _achats;
    private readonly IServiceDocumentsPdf _pdf;
    private readonly IServiceExportExcel _excel;
    private readonly ISessionUtilisateur _session;

    public VueModeleAchats(
        IServiceAchats achats,
        IServiceDocumentsPdf pdf,
        IServiceExportExcel excel,
        ISessionUtilisateur session,
        IServiceDialogue dialogue,
        ILogger<VueModeleAchats> journal)
        : base(dialogue, journal)
    {
        _achats = achats;
        _pdf = pdf;
        _excel = excel;
        _session = session;

        _dateDebut = DateTime.Now.Date.AddMonths(-3);
        _dateFin = DateTime.Now.Date;
    }

    public override string Titre => "Achats";

    public override string SousTitre => "Commandes fournisseurs et réception de la marchandise";

    public ObservableCollection<ResumeAchatDto> Achats { get; } = [];

    public IReadOnlyList<StatutAchat> Statuts { get; } = Enum.GetValues<StatutAchat>();

    [ObservableProperty]
    private ResumeAchatDto? _achatSelectionne;

    [ObservableProperty]
    private AchatDto? _detail;

    [ObservableProperty]
    private string _recherche = string.Empty;

    [ObservableProperty]
    private DateTime? _dateDebut;

    [ObservableProperty]
    private DateTime? _dateFin;

    [ObservableProperty]
    private StatutAchat? _statutFiltre;

    [ObservableProperty]
    private decimal _totalPeriode;

    [ObservableProperty]
    private decimal _resteAPayer;

    public bool PeutGerer => _session.Possede(CodesPermissions.GererAchats);

    /// <summary>Demande l'ouverture du formulaire de commande.</summary>
    public event EventHandler? FormulaireAchatDemande;

    /// <summary>Demande l'ouverture de la fenêtre de réception.</summary>
    public event EventHandler<AchatDto>? ReceptionDemandee;

    public override async Task ChargerAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "chargement des achats")
            .ConfigureAwait(true);

    private async Task ChargerListeAsync()
    {
        var periode = ConstruirePeriode();

        var resultats = await _achats.RechercherAsync(
            periode.DebutUtc,
            periode.FinUtc,
            statut: StatutFiltre,
            recherche: Recherche).ConfigureAwait(true);

        var idPrecedent = AchatSelectionne?.Id;

        Achats.Clear();
        foreach (var achat in resultats)
        {
            Achats.Add(achat);
        }

        var retenus = resultats.Where(a => a.Statut != StatutAchat.Annule).ToList();

        TotalPeriode = retenus.Sum(a => a.Total);
        ResteAPayer = retenus.Sum(a => a.MontantRestant);

        AchatSelectionne = idPrecedent is null
            ? Achats.FirstOrDefault()
            : Achats.FirstOrDefault(a => a.Id == idPrecedent) ?? Achats.FirstOrDefault();
    }

    private Periode ConstruirePeriode() =>
        CalculateurPeriode.Construire(
            TypePeriode.Personnalisee,
            DateTime.Now,
            DateDebut ?? DateTime.Now.Date.AddYears(-5),
            DateFin ?? DateTime.Now.Date);

    partial void OnAchatSelectionneChanged(ResumeAchatDto? value)
    {
        _ = ExecuterAsync(() => ChargerDetailAsync(value?.Id),
            contexteJournal: "chargement du détail d'une commande");
    }

    private async Task ChargerDetailAsync(int? achatId)
    {
        Detail = achatId is null
            ? null
            : await _achats.ObtenirAsync(achatId.Value).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RechercherAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "recherche d'achats").ConfigureAwait(true);

    [RelayCommand]
    private async Task ReinitialiserFiltresAsync()
    {
        Recherche = string.Empty;
        StatutFiltre = null;
        DateDebut = DateTime.Now.Date.AddMonths(-3);
        DateFin = DateTime.Now.Date;

        await ExecuterAsync(ChargerListeAsync, contexteJournal: "réinitialisation des filtres")
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private void NouvelleCommande()
    {
        if (!PeutGerer)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation de créer une commande fournisseur.",
                "Action non autorisée");

            return;
        }

        FormulaireAchatDemande?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Receptionner()
    {
        if (Detail is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une commande.", "Aucune sélection");
            return;
        }

        if (!PeutGerer)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation de réceptionner une commande.",
                "Action non autorisée");

            return;
        }

        if (!Detail.PeutEtreReceptionne)
        {
            Dialogue.Informer(
                "Cette commande n'a plus d'article à réceptionner.",
                "Commande complète");

            return;
        }

        ReceptionDemandee?.Invoke(this, Detail);
    }

    [RelayCommand]
    private async Task ReceptionnerToutAsync()
    {
        if (Detail is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une commande.", "Aucune sélection");
            return;
        }

        if (!PeutGerer)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation de réceptionner une commande.",
                "Action non autorisée");

            return;
        }

        if (!Dialogue.Confirmer(
                $"Réceptionner la totalité de la commande {Detail.NumeroAchat} ?" +
                Environment.NewLine +
                "Toutes les quantités restantes seront ajoutées au stock.",
                "Réception complète"))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _achats.ReceptionnerToutAsync(Detail.Id).ConfigureAwait(true);
                await ChargerListeAsync().ConfigureAwait(true);
            },
            "Marchandise réceptionnée. Le stock a été mis à jour.",
            "réception d'une commande").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task EnregistrerReglementAsync()
    {
        if (Detail is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une commande.", "Aucune sélection");
            return;
        }

        if (Detail.MontantRestant <= 0)
        {
            Dialogue.Informer("Cette commande est déjà réglée en totalité.", "Commande réglée");
            return;
        }

        var saisie = Dialogue.DemanderTexte(
            $"Reste à payer : {FormatageMontant.Formater(Detail.MontantRestant)}." +
            Environment.NewLine + "Montant versé au fournisseur :",
            "Enregistrer un règlement",
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
                await _achats.EnregistrerReglementAsync(Detail.Id, montant).ConfigureAwait(true);
                await ChargerListeAsync().ConfigureAwait(true);
            },
            "Règlement enregistré avec succès.",
            "règlement d'une commande").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AnnulerCommandeAsync()
    {
        if (Detail is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une commande.", "Aucune sélection");
            return;
        }

        if (!PeutGerer)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation d'annuler une commande.",
                "Action non autorisée");

            return;
        }

        var motif = Dialogue.DemanderTexte(
            $"Indiquez le motif de l'annulation de la commande {Detail.NumeroAchat}.",
            "Annuler la commande");

        if (string.IsNullOrWhiteSpace(motif))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _achats.AnnulerAchatAsync(Detail.Id, motif).ConfigureAwait(true);
                await ChargerListeAsync().ConfigureAwait(true);
            },
            "Commande annulée avec succès.",
            "annulation d'une commande").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ImprimerBonAsync()
    {
        if (Detail is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une commande.", "Aucune sélection");
            return;
        }

        await ExecuterAsync(async () =>
        {
            var contenu = await _pdf.GenererBonCommandeAsync(Detail).ConfigureAwait(true);

            await EnregistrerAsync(
                contenu,
                $"Bon-commande-{Detail.NumeroAchat}.pdf",
                "Document PDF (*.pdf)|*.pdf").ConfigureAwait(true);
        }, contexteJournal: "impression d'un bon de commande").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ExporterAsync() =>
        await ExecuterAsync(async () =>
        {
            var contenu = await _excel
                .ExporterAchatsAsync(Achats.ToList(), ConstruirePeriode())
                .ConfigureAwait(true);

            await EnregistrerAsync(
                contenu,
                $"Achats-{DateTime.Now:yyyy-MM-dd}.xlsx",
                "Classeur Excel (*.xlsx)|*.xlsx").ConfigureAwait(true);
        }, contexteJournal: "export des achats").ConfigureAwait(true);

    /// <summary>Recharge la liste après une création ou une réception.</summary>
    public async Task RechargerAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "rechargement des achats")
            .ConfigureAwait(true);

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
