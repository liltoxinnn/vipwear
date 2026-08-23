using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;
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

/// <summary>Article présent dans le panier de la caisse.</summary>
public partial class LignePanier : ObservableObject
{
    public required int VarianteProduitId { get; init; }

    public required string Designation { get; init; }

    public required string Sku { get; init; }

    public string? CodeBarres { get; init; }

    /// <summary>Quantité disponible au moment de l'ajout, pour un contrôle immédiat.</summary>
    public required int StockDisponible { get; set; }

    [ObservableProperty]
    private int _quantite = 1;

    [ObservableProperty]
    private decimal _prixUnitaire;

    [ObservableProperty]
    private decimal _remise;

    /// <summary>Montant de la ligne, remise déduite.</summary>
    public decimal Total => (PrixUnitaire * Quantite) - Remise;

    /// <summary>Vrai lorsque la quantité demandée dépasse le stock connu.</summary>
    public bool DepasseStock => Quantite > StockDisponible;

    partial void OnQuantiteChanged(int value) => Notifier();

    partial void OnPrixUnitaireChanged(decimal value) => Notifier();

    partial void OnRemiseChanged(decimal value) => Notifier();

    /// <summary>
    /// Vrai pendant la notification, pour couper court à un aller-retour
    /// entre la ligne et le panier. Une récursion sans fin sature la pile
    /// d'appels, ce que Windows sanctionne en arrêtant le programme
    /// instantanément, sans erreur ni trace.
    /// </summary>
    private bool _notificationEnCours;

    private void Notifier()
    {
        if (_notificationEnCours)
        {
            return;
        }

        _notificationEnCours = true;

        try
        {
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(DepasseStock));

            LigneModifiee?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _notificationEnCours = false;
        }
    }

    /// <summary>Déclenché à chaque modification qui change le total du panier.</summary>
    public event EventHandler? LigneModifiee;
}

/// <summary>
/// Écran de caisse : scanner, ajouter au panier, vérifier, encaisser, valider.
/// Le flux est optimisé pour le clavier et la douchette : le champ de scan
/// garde le focus et chaque code scanné ajoute directement l'article.
/// </summary>
public partial class VueModeleCaisse : VueModeleBase
{
    private readonly IServiceProduits _produits;
    private readonly IServiceVentes _ventes;
    private readonly IServiceClients _clients;
    private readonly IServiceDocumentsPdf _documents;
    private readonly ISessionUtilisateur _session;

    public VueModeleCaisse(
        IServiceProduits produits,
        IServiceVentes ventes,
        IServiceClients clients,
        IServiceDocumentsPdf documents,
        ISessionUtilisateur session,
        IServiceDialogue dialogue,
        ILogger<VueModeleCaisse> journal)
        : base(dialogue, journal)
    {
        _produits = produits;
        _ventes = ventes;
        _clients = clients;
        _documents = documents;
        _session = session;

        Panier.CollectionChanged += (_, _) => RecalculerTotaux();
    }

    public override string Titre => "Caisse";

    public override string SousTitre => "Scannez un article ou recherchez-le par nom";

    // --- Panier ---

    public ObservableCollection<LignePanier> Panier { get; } = [];

    public ObservableCollection<VarianteDto> ResultatsRecherche { get; } = [];

    /// <summary>
    /// Articles proposés en permanence, sans recherche ni scan. Le caissier
    /// qui connaît son rayon les prend directement, ce qui est plus rapide
    /// que de taper un nom, et indispensable pour les articles dont
    /// l'étiquette est arrachée.
    /// </summary>
    public ObservableCollection<VarianteDto> Catalogue { get; } = [];

    /// <summary>Vrai tant qu'aucun article n'est disponible à la vente.</summary>
    [ObservableProperty]
    private bool _catalogueVide;

    public ObservableCollection<ClientDto> Clients { get; } = [];

    public IReadOnlyList<ModePaiement> ModesPaiement { get; } = Enum.GetValues<ModePaiement>();

    // --- Saisie ---

    [ObservableProperty]
    private string _codeScanne = string.Empty;

    [ObservableProperty]
    private string _termeRecherche = string.Empty;

    [ObservableProperty]
    private LignePanier? _ligneSelectionnee;

    [ObservableProperty]
    private ClientDto? _clientSelectionne;

    // --- Totaux ---

    [ObservableProperty]
    private decimal _sousTotal;

    [ObservableProperty]
    private decimal _remiseGlobale;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private int _nombreArticles;

    // --- Règlement ---

    [ObservableProperty]
    private ModePaiement _modePaiement = ModePaiement.Especes;

    [ObservableProperty]
    private decimal _montantRecu;

    [ObservableProperty]
    private decimal _monnaieARendre;

    /// <summary>Vrai lorsque l'utilisateur a le droit d'accorder une remise.</summary>
    public bool PeutAccorderRemise => _session.Possede(CodesPermissions.AccorderRemise);

    /// <summary>Vrai lorsque le panier contient au moins un article.</summary>
    public bool PanierNonVide => Panier.Count > 0;

    public override async Task ChargerAsync() =>
        await ExecuterAsync(async () =>
        {
            var clients = await _clients.RechercherAsync(limite: 300).ConfigureAwait(true);

            Clients.Clear();
            foreach (var client in clients)
            {
                Clients.Add(client);
            }

            await ChargerCatalogueAsync().ConfigureAwait(true);
        }, contexteJournal: "chargement de la caisse").ConfigureAwait(true);

    /// <summary>Recharge la liste des articles proposés en caisse.</summary>
    private async Task ChargerCatalogueAsync()
    {
        var articles = await _produits.ListerVendablesAsync().ConfigureAwait(true);

        Catalogue.Clear();
        foreach (var article in articles)
        {
            Catalogue.Add(article);
        }

        CatalogueVide = Catalogue.Count == 0;
    }

    // ==================================================================
    // Ajout d'articles
    // ==================================================================

    /// <summary>
    /// Traite un code-barres scanné. Si l'article est déjà au panier, sa
    /// quantité est simplement incrémentée : scanner deux fois le même
    /// article ne crée pas deux lignes.
    /// </summary>
    [RelayCommand]
    private async Task ScannerAsync()
    {
        var code = CodeScanne.Trim();

        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        CodeScanne = string.Empty;

        await ExecuterAsync(async () =>
        {
            var variante = await _produits.ObtenirVarianteParCodeBarresAsync(code).ConfigureAwait(true);

            if (variante is null)
            {
                // Aucun code-barres ne correspond : on tente une recherche
                // libre, le personnel saisit parfois un code article.
                var resultats = await _produits.RechercherVariantesAsync(code, 10).ConfigureAwait(true);

                if (resultats.Count == 1)
                {
                    AjouterAuPanier(resultats[0]);
                    return;
                }

                if (resultats.Count > 1)
                {
                    AfficherResultats(resultats);
                    MessageStatut = $"{resultats.Count} articles correspondent à « {code} ».";
                    return;
                }

                Dialogue.Avertir(
                    $"Aucun article ne correspond au code « {code} ».",
                    "Article introuvable");

                return;
            }

            AjouterAuPanier(variante);
        }, contexteJournal: "scan d'un article").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RechercherAsync()
    {
        var terme = TermeRecherche.Trim();

        if (terme.Length < 2)
        {
            ResultatsRecherche.Clear();
            return;
        }

        await ExecuterAsync(async () =>
        {
            var resultats = await _produits.RechercherVariantesAsync(terme, 40).ConfigureAwait(true);

            AfficherResultats(resultats);

            if (resultats.Count == 0)
            {
                MessageStatut = $"Aucun article ne correspond à « {terme} ».";
            }
        }, contexteJournal: "recherche d'articles en caisse").ConfigureAwait(true);
    }

    /// <summary>Referme les résultats et réaffiche tout le rayon.</summary>
    [RelayCommand]
    private void EffacerRecherche()
    {
        TermeRecherche = string.Empty;
        ResultatsRecherche.Clear();
        MessageStatut = null;
    }

    [RelayCommand]
    private void AjouterArticle(VarianteDto? variante)
    {
        if (variante is not null)
        {
            AjouterAuPanier(variante);
        }
    }

    private void AjouterAuPanier(VarianteDto variante)
    {
        if (variante.QuantiteDisponible <= 0)
        {
            Dialogue.Avertir(
                $"L'article « {variante.Sku} » est en rupture de stock.",
                "Stock épuisé");

            return;
        }

        var existante = Panier.FirstOrDefault(l => l.VarianteProduitId == variante.Id);

        if (existante is not null)
        {
            if (existante.Quantite + 1 > variante.QuantiteDisponible)
            {
                Dialogue.Avertir(
                    $"Stock insuffisant pour cet article. Il ne reste que " +
                    $"{variante.QuantiteDisponible} exemplaire(s) de « {variante.Sku} ».",
                    "Stock insuffisant");

                return;
            }

            existante.StockDisponible = variante.QuantiteDisponible;
            existante.Quantite++;

            LigneSelectionnee = existante;
            RecalculerTotaux();

            return;
        }

        var ligne = new LignePanier
        {
            VarianteProduitId = variante.Id,
            Designation = variante.Designation,
            Sku = variante.Sku,
            CodeBarres = variante.CodeBarres,
            StockDisponible = variante.QuantiteDisponible,
            Quantite = 1,
            PrixUnitaire = variante.PrixVente
        };

        ligne.LigneModifiee += (_, _) => RecalculerTotaux();

        Panier.Add(ligne);
        LigneSelectionnee = ligne;

        MessageStatut = $"Article ajouté : {variante.Sku}";
    }

    private void AfficherResultats(IReadOnlyList<VarianteDto> resultats)
    {
        ResultatsRecherche.Clear();

        foreach (var resultat in resultats)
        {
            ResultatsRecherche.Add(resultat);
        }
    }

    // ==================================================================
    // Modification du panier
    // ==================================================================

    [RelayCommand]
    private void AugmenterQuantite(LignePanier? ligne)
    {
        if (ligne is null)
        {
            return;
        }

        if (ligne.Quantite + 1 > ligne.StockDisponible)
        {
            Dialogue.Avertir(
                $"Stock insuffisant pour cet article. Il ne reste que " +
                $"{ligne.StockDisponible} exemplaire(s).",
                "Stock insuffisant");

            return;
        }

        ligne.Quantite++;
    }

    [RelayCommand]
    private void DiminuerQuantite(LignePanier? ligne)
    {
        if (ligne is null)
        {
            return;
        }

        if (ligne.Quantite <= 1)
        {
            RetirerLigne(ligne);
            return;
        }

        ligne.Quantite--;
    }

    [RelayCommand]
    private void RetirerLigne(LignePanier? ligne)
    {
        if (ligne is null)
        {
            return;
        }

        Panier.Remove(ligne);
        RecalculerTotaux();
    }

    [RelayCommand]
    private void ViderPanier()
    {
        if (Panier.Count == 0)
        {
            return;
        }

        if (!Dialogue.Confirmer(
                "Êtes-vous sûr de vouloir vider le panier ? Tous les articles saisis seront retirés.",
                "Vider le panier"))
        {
            return;
        }

        Panier.Clear();
        RemiseGlobale = 0m;
        MontantRecu = 0m;
        ClientSelectionne = null;
        MessageStatut = null;

        RecalculerTotaux();
    }

    /// <summary>
    /// Vrai pendant un recalcul. Le recalcul modifie des propriétés qui, à
    /// leur tour, demandent un recalcul : sans ce verrou, une remise ou une
    /// quantité particulière peut enclencher un enchaînement sans fin.
    /// </summary>
    private bool _recalculEnCours;

    private void RecalculerTotaux()
    {
        if (_recalculEnCours)
        {
            Journal.LogWarning(
                "Recalcul des totaux réentrant : l'enchaînement a été interrompu. " +
                "Remise {Remise}, sous-total {SousTotal}.", RemiseGlobale, SousTotal);

            return;
        }

        _recalculEnCours = true;

        try
        {
            RecalculerTotauxInterne();
        }
        finally
        {
            _recalculEnCours = false;
        }
    }

    private void RecalculerTotauxInterne()
    {
        SousTotal = Panier.Sum(l => l.Total);
        NombreArticles = Panier.Sum(l => l.Quantite);

        // Une remise ne peut jamais rendre le ticket négatif.
        if (RemiseGlobale > SousTotal)
        {
            RemiseGlobale = SousTotal;
        }

        Total = SousTotal - RemiseGlobale;

        CalculerMonnaie();

        OnPropertyChanged(nameof(PanierNonVide));
        ValiderVenteCommand.NotifyCanExecuteChanged();
    }

    partial void OnRemiseGlobaleChanged(decimal value) => RecalculerTotaux();

    partial void OnMontantRecuChanged(decimal value) => CalculerMonnaie();

    private void CalculerMonnaie() =>
        MonnaieARendre = MontantRecu > Total ? MontantRecu - Total : 0m;

    /// <summary>Prépare l'appoint exact, geste le plus fréquent en caisse.</summary>
    [RelayCommand]
    private void MontantExact() => MontantRecu = Total;

    // ==================================================================
    // Encaissement
    // ==================================================================

    private bool PeutValiderVente() => Panier.Count > 0 && !EstOccupe;

    /// <summary>
    /// Encaisse le panier.
    ///
    /// Tout est enveloppé : une erreur imprévue doit se voir dans un message,
    /// jamais fermer la caisse en pleine vente. Le geste le plus critique du
    /// logiciel ne peut pas être le moins protégé.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PeutValiderVente))]
    private async Task ValiderVenteAsync()
    {
        try
        {
            await EncaisserAsync().ConfigureAwait(true);
        }
        catch (Domain.Exceptions.ExceptionMetier erreur)
        {
            Journal.LogWarning(erreur, "Vente refusée : {Message}", erreur.Message);
            Dialogue.Erreur(erreur.Message, "Vente impossible");
        }
        catch (Exception erreur)
        {
            Journal.LogError(erreur, "Vente : erreur imprévue en caisse.");

            Dialogue.Erreur(
                "L'encaissement s'est interrompu." + Environment.NewLine + Environment.NewLine +
                "Détail : " + erreur.Message + Environment.NewLine + Environment.NewLine +
                "Vérifiez dans l'écran Ventes si l'opération a été enregistrée avant " +
                "de la refaire, afin de ne pas la compter deux fois.",
                "Encaissement interrompu");
        }
    }

    private async Task EncaisserAsync()
    {
        if (Panier.Count == 0)
        {
            Dialogue.Avertir("Le panier est vide. Ajoutez au moins un article.", "Panier vide");
            return;
        }

        // Le montant encaissé est plafonné au montant dû : la monnaie rendue
        // ne fait pas partie de la recette.
        var montantRegle = MontantRecu >= Total ? Total : MontantRecu;

        if (montantRegle < Total)
        {
            var manquant = Total - montantRegle;

            if (!Dialogue.Confirmer(
                    $"Le règlement est incomplet : il manque " +
                    $"{FormatageMontant.Formater(manquant)}." + Environment.NewLine + Environment.NewLine +
                    "Voulez-vous enregistrer cette vente comme partiellement payée ?",
                    "Règlement incomplet"))
            {
                return;
            }
        }

        var demande = new DemandeVente
        {
            ClientId = ClientSelectionne?.Id,
            RemiseGlobale = RemiseGlobale,
            Lignes = Panier.Select(l => new LigneVenteDemande
            {
                VarianteProduitId = l.VarianteProduitId,
                Quantite = l.Quantite,
                PrixUnitaire = l.PrixUnitaire,
                Remise = l.Remise
            }).ToList(),
            Paiements = montantRegle > 0
                ?
                [
                    new PaiementDemande { ModePaiement = ModePaiement, Montant = montantRegle }
                ]
                : []
        };

        // Chaque étape est tracée. Une vente qui échoue en caisse doit pouvoir
        // être diagnostiquée sur le poste du magasin, où l'on ne dispose que
        // du journal.
        Journal.LogInformation("Vente : enregistrement de {Lignes} ligne(s) pour {Total}.",
            demande.Lignes.Count, Total);

        var vente = await ExecuterAsync(
            () => _ventes.EnregistrerVenteAsync(demande),
            contexteJournal: "validation d'une vente").ConfigureAwait(true);

        if (vente is null)
        {
            Journal.LogWarning("Vente : enregistrement abandonné, rien n'a été écrit.");
            return;
        }

        Journal.LogInformation("Vente {Numero} enregistrée. Stock décompté.", vente.NumeroVente);

        var monnaie = MonnaieARendre;

        // À partir d'ici la vente EST enregistrée et le stock décompté. Plus
        // rien ne doit pouvoir faire échouer l'opération : ce qui suit ne
        // concerne que l'affichage et l'impression.
        try
        {
            // Le panier est vidé immédiatement : la caisse est prête pour le
            // client suivant.
            Panier.Clear();
            RemiseGlobale = 0m;
            MontantRecu = 0m;
            ClientSelectionne = null;
            ResultatsRecherche.Clear();
            TermeRecherche = string.Empty;
            RecalculerTotaux();

            // Les quantités affichées dans le catalogue viennent d'être décomptées.
            await ChargerCatalogueAsync().ConfigureAwait(true);
        }
        catch (Exception erreur)
        {
            Journal.LogError(erreur, "Vente {Numero} : remise à zéro de la caisse en échec.",
                vente.NumeroVente);
        }

        var message = $"Vente {vente.NumeroVente} enregistrée pour " +
                      $"{FormatageMontant.Formater(vente.Total)}.";

        if (monnaie > 0)
        {
            message += Environment.NewLine + $"Monnaie à rendre : {FormatageMontant.Formater(monnaie)}";
        }

        MessageStatut = message;

        if (Dialogue.Confirmer(
                message + Environment.NewLine + Environment.NewLine + "Voulez-vous imprimer le ticket ?",
                "Vente enregistrée"))
        {
            await ImprimerTicketAsync(vente).ConfigureAwait(true);
        }

        Journal.LogInformation("Vente {Numero} : caisse prête pour le client suivant.",
            vente.NumeroVente);
    }

    /// <summary>
    /// Produit le ticket de la vente.
    ///
    /// La vente est déjà enregistrée à ce stade : un ticket qui échoue ne doit
    /// jamais la remettre en cause. Les étapes sont tracées séparément car la
    /// mise en page du PDF s'appuie sur une bibliothèque native, dont un
    /// incident ne laisse aucune exception derrière lui.
    /// </summary>
    private async Task ImprimerTicketAsync(VenteDto vente)
    {
        var chemin = Dialogue.DemanderCheminEnregistrement(
            $"Ticket-{vente.NumeroVente}.pdf",
            "Document PDF (*.pdf)|*.pdf");

        if (chemin is null)
        {
            return;
        }

        byte[] contenu;

        try
        {
            Journal.LogInformation("Ticket {Numero} : mise en page du document…", vente.NumeroVente);

            contenu = await _documents.GenererTicketAsync(vente).ConfigureAwait(true);

            Journal.LogInformation("Ticket {Numero} : document produit ({Taille} octets).",
                vente.NumeroVente, contenu.Length);
        }
        catch (Exception erreur)
        {
            Journal.LogError(erreur, "Ticket {Numero} : mise en page impossible.", vente.NumeroVente);

            Dialogue.Erreur(
                $"La vente {vente.NumeroVente} est bien enregistrée, mais le ticket " +
                "n'a pas pu être produit." + Environment.NewLine + Environment.NewLine +
                "Vous pouvez le réimprimer depuis l'écran Ventes.",
                "Ticket non imprimé");

            return;
        }

        try
        {
            await File.WriteAllBytesAsync(chemin, contenu).ConfigureAwait(true);

            OuvrirDocument(chemin);
        }
        catch (Exception erreur)
        {
            Journal.LogError(erreur, "Ticket {Numero} : enregistrement ou ouverture impossible.",
                vente.NumeroVente);

            Dialogue.Avertir(
                $"La vente {vente.NumeroVente} est enregistrée et le ticket a été créé, " +
                "mais il n'a pas pu être ouvert." + Environment.NewLine + Environment.NewLine +
                $"Le fichier se trouve ici : {chemin}",
                "Ticket non ouvert");
        }
    }

    /// <summary>Ouvre le document avec le lecteur PDF installé sur le poste.</summary>
    internal static void OuvrirDocument(string chemin)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = chemin,
            UseShellExecute = true
        });
    }
}
