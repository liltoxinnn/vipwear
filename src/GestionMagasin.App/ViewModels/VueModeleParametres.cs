using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Converters;
using GestionMagasin.App.Impression;
using GestionMagasin.App.Services;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Impression;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.ServeurEmbarque;
using GestionMagasin.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>
/// Paramètres du magasin : identité, mentions légales, devise, TVA et délai
/// de retour, ainsi que la gestion des marques, tailles et couleurs.
/// </summary>
public partial class VueModeleParametres : VueModeleBase
{
    private readonly IServiceParametres _parametres;
    private readonly IServiceProduits _produits;
    private readonly IServiceImpressionTicket _impression;

    /// <summary>
    /// Absent lorsque le magasin utilise un PostgreSQL installé sur la
    /// machine : pgAdmin est alors disponible pour les sauvegardes. Présent
    /// dès que le logiciel héberge sa propre base, cas où ces boutons sont la
    /// seule protection des données du magasin.
    /// </summary>
    private readonly ServiceSauvegarde? _sauvegarde;

    public VueModeleParametres(
        IServiceParametres parametres,
        IServiceProduits produits,
        IServiceImpressionTicket impression,
        IServiceDialogue dialogue,
        ILogger<VueModeleParametres> journal,
        ServiceSauvegarde? sauvegarde = null)
        : base(dialogue, journal)
    {
        _parametres = parametres;
        _produits = produits;
        _impression = impression;
        _sauvegarde = sauvegarde;
    }

    /// <summary>Vrai lorsque la sauvegarde est assurée par le logiciel lui-même.</summary>
    public bool SauvegardeDisponible => _sauvegarde is not null;

    public override string Titre => "Paramètres";

    public override string SousTitre => "Informations du magasin et données de référence";

    // --- Fiche magasin ---

    [ObservableProperty]
    private string _nomMagasin = string.Empty;

    [ObservableProperty]
    private string _adresse = string.Empty;

    [ObservableProperty]
    private string _telephone = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _devise = "DZD";

    [ObservableProperty]
    private string _symboleDevise = "DA";

    [ObservableProperty]
    private string _informationsTicket = string.Empty;

    /// <summary>Lien imprimé en QR code en bas des tickets ; vide, pas de QR code.</summary>
    [ObservableProperty]
    private string _lienQrCodeTicket = string.Empty;

    [ObservableProperty]
    private string _registreCommerce = string.Empty;

    [ObservableProperty]
    private string _numeroIdentificationFiscale = string.Empty;

    [ObservableProperty]
    private string _articleImposition = string.Empty;

    [ObservableProperty]
    private decimal _tauxTva = 19m;

    [ObservableProperty]
    private int _delaiRetourJours = 30;

    // --- Données de référence ---

    public ObservableCollection<ReferenceDto> Marques { get; } = [];

    /// <summary>Familles d'articles, avec le système de tailles de chacune.</summary>
    public ObservableCollection<CategorieDto> Familles { get; } = [];

    /// <summary>Systèmes de tailles : lettres, tailles de pantalon, pointures.</summary>
    public ObservableCollection<ReferenceDto> SystemesTailles { get; } = [];

    public ObservableCollection<ReferenceDto> Tailles { get; } = [];

    public ObservableCollection<ReferenceDto> Couleurs { get; } = [];

    [ObservableProperty]
    private ReferenceDto? _marqueSelectionnee;

    [ObservableProperty]
    private ReferenceDto? _tailleSelectionnee;

    [ObservableProperty]
    private ReferenceDto? _couleurSelectionnee;

    public override async Task ChargerAsync() =>
        await ExecuterAsync(async () =>
        {
            var magasin = await _parametres.ObtenirAsync().ConfigureAwait(true);

            NomMagasin = magasin.NomMagasin;
            Adresse = magasin.Adresse ?? string.Empty;
            Telephone = magasin.Telephone ?? string.Empty;
            Email = magasin.Email ?? string.Empty;
            Devise = magasin.Devise;
            SymboleDevise = magasin.SymboleDevise;
            InformationsTicket = magasin.InformationsTicket ?? string.Empty;
            LienQrCodeTicket = magasin.LienQrCodeTicket ?? string.Empty;
            RegistreCommerce = magasin.RegistreCommerce ?? string.Empty;
            NumeroIdentificationFiscale = magasin.NumeroIdentificationFiscale ?? string.Empty;
            ArticleImposition = magasin.ArticleImposition ?? string.Empty;
            TauxTva = magasin.TauxTva;
            DelaiRetourJours = magasin.DelaiRetourJours;

            await ChargerReferencesAsync().ConfigureAwait(true);

            ChargerImprimante();
        }, contexteJournal: "chargement des paramètres").ConfigureAwait(true);

    // ==================================================================
    // Imprimante de tickets
    // ==================================================================

    /// <summary>Imprimantes installées dans Windows sur ce poste.</summary>
    public ObservableCollection<string> Imprimantes { get; } = [];

    /// <summary>Choix d'exemplaires proposés : au-delà de trois, c'est une erreur de saisie.</summary>
    public IReadOnlyList<int> ChoixExemplaires { get; } = [1, 2, 3];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EtatImprimante))]
    [NotifyPropertyChangedFor(nameof(ImprimanteChoisieUtilisable))]
    private string? _imprimanteChoisie;

    /// <summary>L'imprimante choisie fabrique des fichiers (PDF, XPS…) au lieu d'imprimer.</summary>
    private bool _choisieVirtuelle;

    partial void OnImprimanteChoisieChanged(string? value) =>
        _choisieVirtuelle = value is not null && _impression.EstVirtuelle(value);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Rouleau58))]
    private bool _rouleau80;

    [ObservableProperty]
    private bool _imprimerSansDemander;

    [ObservableProperty]
    private bool _couperPapier = true;

    [ObservableProperty]
    private bool _ouvrirTiroir;

    [ObservableProperty]
    private bool _imprimerLogo = true;

    [ObservableProperty]
    private int _exemplaires = 1;

    /// <summary>
    /// Pendant de <see cref="Rouleau80"/> pour le second bouton radio. Seul
    /// le passage à vrai est pris en compte : c'est le groupe de boutons qui
    /// décoche l'autre.
    /// </summary>
    public bool Rouleau58
    {
        get => !Rouleau80;
        set
        {
            if (value)
            {
                Rouleau80 = false;
            }
        }
    }

    /// <summary>
    /// Faux lorsque l'imprimante enregistrée ne peut pas imprimer de ticket :
    /// c'est une imprimante PDF, ou elle n'apparaît plus dans Windows —
    /// renommée, débranchée et désinstallée, ou installée sous un autre nom
    /// après une réinstallation du pilote.
    /// </summary>
    public bool ImprimanteChoisieUtilisable =>
        ImprimanteChoisie is null || (!_choisieVirtuelle && Imprimantes.Contains(ImprimanteChoisie));

    /// <summary>Ce que fera la caisse, en une phrase.</summary>
    public string EtatImprimante => ImprimanteChoisie is null
        ? "Aucune imprimante choisie : les tickets sont produits en PDF."
        : _choisieVirtuelle
            ? $"« {ImprimanteChoisie} » n'est pas une imprimante de tickets : elle fabrique un fichier " +
              "PDF, qui sortirait vide (0 Ko). Choisissez l'imprimante de tickets dans la liste, ou " +
              "« Ne plus utiliser d'imprimante » pour avoir les tickets en PDF."
            : ImprimanteChoisieUtilisable
                ? $"Les tickets partent directement sur « {ImprimanteChoisie} »."
                : $"« {ImprimanteChoisie} » n'est plus installée dans Windows. " +
                  "Choisissez-la de nouveau dans la liste.";

    private void ChargerImprimante()
    {
        var reglages = _impression.Reglages;

        RemplirImprimantes(reglages.NomImprimante);

        ImprimanteChoisie = reglages.NomImprimante;
        Rouleau80 = reglages.Largeur == LargeurRouleau.Rouleau80;
        ImprimerSansDemander = reglages.ImprimerSansDemander;
        CouperPapier = reglages.CouperPapier;
        OuvrirTiroir = reglages.OuvrirTiroir;
        ImprimerLogo = reglages.ImprimerLogo;
        Exemplaires = reglages.Exemplaires;
    }

    /// <summary>
    /// Remplit la liste. L'imprimante enregistrée y figure même si Windows ne
    /// la connaît plus : la retirer en silence ferait croire qu'aucune n'a
    /// jamais été choisie.
    /// </summary>
    private void RemplirImprimantes(string? aConserver)
    {
        Imprimantes.Clear();

        foreach (var nom in _impression.ListerImprimantes())
        {
            Imprimantes.Add(nom);
        }

        if (aConserver is not null && !Imprimantes.Contains(aConserver))
        {
            Imprimantes.Add(aConserver);
        }

        OnPropertyChanged(nameof(ImprimanteChoisieUtilisable));
        OnPropertyChanged(nameof(EtatImprimante));
    }

    private ReglagesTicket ReglagesSaisis() => new ReglagesTicket
    {
        NomImprimante = ImprimanteChoisie,
        Largeur = Rouleau80 ? LargeurRouleau.Rouleau80 : LargeurRouleau.Rouleau58,
        ImprimerSansDemander = ImprimerSansDemander,
        CouperPapier = CouperPapier,
        OuvrirTiroir = OuvrirTiroir,
        ImprimerLogo = ImprimerLogo,
        Exemplaires = Exemplaires
    }.Normaliser();

    [RelayCommand]
    private void ActualiserImprimantes()
    {
        var choisie = ImprimanteChoisie;

        RemplirImprimantes(choisie);

        // Le remplissage vide la liste, et la liste déroulante perd alors sa
        // sélection : elle est rétablie.
        ImprimanteChoisie = choisie;

        if (Imprimantes.Count == 0)
        {
            Dialogue.Avertir(
                "Windows ne signale aucune imprimante. Vérifiez que l'imprimante est branchée et " +
                "allumée, et que son pilote a été installé, puis actualisez la liste.",
                "Aucune imprimante");
        }
    }

    [RelayCommand]
    private void EnregistrerImprimante()
    {
        if (Sauvegarder())
        {
            Dialogue.Informer(
                ImprimanteChoisie is null
                    ? "Aucune imprimante n'est choisie : les tickets seront produits en PDF."
                    : $"Les tickets de ce poste partiront sur « {ImprimanteChoisie} ».",
                "Imprimante de tickets");
        }
    }

    private bool Sauvegarder()
    {
        if (_choisieVirtuelle)
        {
            Dialogue.Avertir(
                $"« {ImprimanteChoisie} » ne peut pas servir d'imprimante de tickets : elle fabrique un " +
                "fichier PDF, et ne comprend pas les commandes d'une imprimante de caisse — chaque ticket " +
                "sortirait en fichier vide, de 0 Ko." +
                Environment.NewLine + Environment.NewLine +
                "Choisissez l'imprimante de tickets dans la liste. Si elle n'y figure pas, installez son " +
                "pilote, puis appuyez sur Actualiser. Pour avoir les tickets en PDF, appuyez sur " +
                "« Ne plus utiliser d'imprimante ».",
                "Imprimante PDF");

            return false;
        }

        try
        {
            _impression.Enregistrer(ReglagesSaisis());

            return true;
        }
        catch (Exception erreur) when (erreur is IOException or UnauthorizedAccessException)
        {
            Journal.LogError(erreur, "Réglages de l'imprimante : enregistrement impossible.");

            Dialogue.Erreur(
                "Les réglages de l'imprimante n'ont pas pu être enregistrés sur ce poste." +
                Environment.NewLine + erreur.Message,
                "Enregistrement impossible");

            return false;
        }
    }

    /// <summary>
    /// Imprime un ticket d'essai avec les réglages affichés, enregistrés ou
    /// non : on règle, on essaie, on corrige, et on n'enregistre que ce qui a
    /// donné un bon ticket.
    /// </summary>
    [RelayCommand]
    private async Task ImprimerEssaiAsync()
    {
        if (ImprimanteChoisie is null)
        {
            Dialogue.Avertir("Choisissez d'abord l'imprimante à essayer.", "Aucune imprimante");
            return;
        }

        try
        {
            await _impression.ImprimerEssaiAsync(ReglagesSaisis()).ConfigureAwait(true);

            // Un essai réussi était suivi d'un oubli : le ticket sortait bien,
            // mais rien n'était enregistré, et la caisse continuait de
            // produire des PDF. La question est posée ici, au moment où l'on
            // a le ticket sous les yeux.
            if (Dialogue.Confirmer(
                    $"Le ticket d'essai a été envoyé à « {ImprimanteChoisie} »." +
                    Environment.NewLine + Environment.NewLine +
                    "Est-il correct — lisible jusqu'au bord, montants alignés à droite, accents de " +
                    "« Veste légère été — Écru » bien imprimés ?" +
                    Environment.NewLine + Environment.NewLine +
                    "Oui : ces réglages sont retenus et les tickets de la caisse partiront sur cette " +
                    "imprimante.",
                    "Ticket d'essai")
                && Sauvegarder())
            {
                Dialogue.Informer(
                    $"Les tickets de ce poste partiront sur « {ImprimanteChoisie} ».",
                    "Imprimante de tickets");
            }
        }
        catch (ImpressionTicketException erreur)
        {
            Journal.LogWarning(erreur, "Ticket d'essai : impression impossible.");
            Dialogue.Avertir(erreur.Message, "Ticket d'essai non imprimé");
        }
        catch (Exception erreur)
        {
            Journal.LogError(erreur, "Ticket d'essai : incident pendant l'impression.");
            Dialogue.Avertir(
                "Le ticket d'essai n'a pas pu être imprimé. Vérifiez que l'imprimante est allumée, " +
                "branchée et approvisionnée en papier.",
                "Ticket d'essai non imprimé");
        }
    }

    /// <summary>Revient au ticket en PDF sur ce poste.</summary>
    [RelayCommand]
    private void RetirerImprimante()
    {
        ImprimanteChoisie = null;
        EnregistrerImprimante();
    }

    private async Task ChargerReferencesAsync()
    {
        Remplir(Marques, await _produits.ListerMarquesAsync(true).ConfigureAwait(true));
        Remplir(Familles, await _produits.ListerCategoriesAsync(true).ConfigureAwait(true));
        Remplir(SystemesTailles, await _produits.ListerSystemesTaillesAsync(true).ConfigureAwait(true));

        // La liste des tailles est cloisonnée par système : mélangées, les
        // lettres, les tailles de pantalon et les pointures formeraient une
        // liste de trente entrées où « 42 » apparaîtrait deux fois sans qu'on
        // sache laquelle est laquelle.
        SystemeTailleChoisi ??= SystemesTailles.FirstOrDefault();

        await ChargerTaillesAsync().ConfigureAwait(true);
        Remplir(Couleurs, await _produits.ListerCouleursAsync(true).ConfigureAwait(true));
    }

    [RelayCommand]
    private async Task EnregistrerAsync() =>
        await ExecuterAsync(
            async () =>
            {
                var magasin = new ParametresMagasin
                {
                    Id = 1,
                    NomMagasin = NomMagasin,
                    Adresse = Adresse,
                    Telephone = Telephone,
                    Email = Email,
                    Devise = Devise,
                    SymboleDevise = SymboleDevise,
                    InformationsTicket = InformationsTicket,
                    LienQrCodeTicket = LienQrCodeTicket,
                    RegistreCommerce = RegistreCommerce,
                    NumeroIdentificationFiscale = NumeroIdentificationFiscale,
                    ArticleImposition = ArticleImposition,
                    TauxTva = TauxTva,
                    DelaiRetourJours = DelaiRetourJours
                };

                var enregistres = await _parametres.EnregistrerAsync(magasin).ConfigureAwait(true);

                // Les montants affichés reprennent immédiatement le nouveau symbole.
                ConvertisseurMontant.SymboleDevise = enregistres.SymboleDevise;
            },
            "Paramètres enregistrés avec succès.",
            "enregistrement des paramètres").ConfigureAwait(true);

    // ==================================================================
    // Marques
    // ==================================================================

    [RelayCommand]
    private async Task NouvelleMarqueAsync()
    {
        var nom = Dialogue.DemanderTexte("Nom de la marque :", "Nouvelle marque");

        if (string.IsNullOrWhiteSpace(nom))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _produits.EnregistrerMarqueAsync(null, nom, null).ConfigureAwait(true);
                await ChargerReferencesAsync().ConfigureAwait(true);
            },
            "Marque ajoutée avec succès.",
            "création d'une marque").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RenommerMarqueAsync()
    {
        if (MarqueSelectionnee is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une marque.", "Aucune sélection");
            return;
        }

        var identifiant = MarqueSelectionnee.Id;

        var nom = Dialogue.DemanderTexte(
            "Nouveau nom de la marque :",
            "Renommer la marque",
            MarqueSelectionnee.Nom);

        if (string.IsNullOrWhiteSpace(nom))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _produits.EnregistrerMarqueAsync(identifiant, nom, null).ConfigureAwait(true);
                await ChargerReferencesAsync().ConfigureAwait(true);
            },
            "Marque modifiée avec succès.",
            "modification d'une marque").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task BasculerEtatMarqueAsync()
    {
        if (MarqueSelectionnee is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une marque.", "Aucune sélection");
            return;
        }

        var identifiant = MarqueSelectionnee.Id;
        var activer = !MarqueSelectionnee.Actif;

        await ExecuterAsync(
            async () =>
            {
                await _produits.DefinirEtatMarqueAsync(identifiant, activer).ConfigureAwait(true);
                await ChargerReferencesAsync().ConfigureAwait(true);
            },
            activer ? "Marque réactivée." : "Marque désactivée.",
            "changement d'état d'une marque").ConfigureAwait(true);
    }

    private async Task ChargerTaillesAsync() =>
        Remplir(Tailles, await _produits
            .ListerTaillesAsync(true, systemeTailleId: SystemeTailleChoisi?.Id)
            .ConfigureAwait(true));

    /// <summary>Système dont les tailles sont affichées.</summary>
    [ObservableProperty]
    private ReferenceDto? _systemeTailleChoisi;

    partial void OnSystemeTailleChoisiChanged(ReferenceDto? value) =>
        _ = ExecuterAsync(ChargerTaillesAsync, contexteJournal: "changement de système de tailles");

    // ==================================================================
    // Familles d'articles
    // ==================================================================

    [ObservableProperty]
    private CategorieDto? _familleSelectionnee;

    [RelayCommand]
    private async Task NouvelleFamilleAsync()
    {
        if (SystemesTailles.Count == 0)
        {
            return;
        }

        var nom = Dialogue.DemanderTexte(
            "Nom de la famille (Casquettes, Ceintures…) :",
            "Nouvelle famille d'articles");

        if (string.IsNullOrWhiteSpace(nom))
        {
            return;
        }

        // Le système est demandé séparément : c'est le choix qui engage, car
        // il fixe les tailles que les articles de cette famille pourront
        // prendre, et il ne se change plus une fois des articles créés.
        var systemes = string.Join(
            Environment.NewLine,
            SystemesTailles.Select((s, i) => $"{i + 1}. {s.Nom}"));

        var choix = Dialogue.DemanderTexte(
            "Quel système de tailles cette famille emploie-t-elle ?" +
            Environment.NewLine + Environment.NewLine + systemes +
            Environment.NewLine + Environment.NewLine + "Numéro :",
            "Système de tailles",
            "1");

        if (!int.TryParse(choix, out var rang) || rang < 1 || rang > SystemesTailles.Count)
        {
            Dialogue.Avertir(
                "Aucun système de tailles valide n'a été choisi : la famille n'a pas été créée.",
                "Famille non créée");

            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _produits.EnregistrerCategorieAsync(
                    null, nom, SystemesTailles[rang - 1].Id, (Familles.Count + 1) * 10)
                    .ConfigureAwait(true);

                await ChargerReferencesAsync().ConfigureAwait(true);
            },
            "Famille ajoutée avec succès.",
            "création d'une famille").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task BasculerEtatFamilleAsync()
    {
        if (FamilleSelectionnee is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une famille.", "Aucune sélection");
            return;
        }

        var famille = FamilleSelectionnee;

        if (famille.Actif && famille.NombreProduits > 0 && !Dialogue.Confirmer(
                $"La famille « {famille.Nom} » porte {famille.NombreProduits} article(s). " +
                "Elle ne sera plus proposée à la création d'un produit, mais les " +
                "articles existants restent intacts. Continuer ?",
                "Désactiver la famille"))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _produits.DefinirEtatCategorieAsync(famille.Id, !famille.Actif).ConfigureAwait(true);
                await ChargerReferencesAsync().ConfigureAwait(true);
            },
            famille.Actif ? "Famille désactivée." : "Famille réactivée.",
            "changement d'état d'une famille").ConfigureAwait(true);
    }

    // ==================================================================
    // Tailles
    // ==================================================================

    [RelayCommand]
    private async Task NouvelleTailleAsync()
    {
        if (SystemeTailleChoisi is null)
        {
            Dialogue.Avertir(
                "Choisissez d'abord le système de tailles auquel la nouvelle taille appartient.",
                "Système de tailles");

            return;
        }

        var nom = Dialogue.DemanderTexte(
            $"Nom de la taille, dans « {SystemeTailleChoisi.Nom} » :",
            "Nouvelle taille");

        if (string.IsNullOrWhiteSpace(nom))
        {
            return;
        }

        var ordreSaisi = Dialogue.DemanderTexte(
            "Position d'affichage (les valeurs les plus petites apparaissent en premier) :",
            "Nouvelle taille",
            ((Tailles.Count + 1) * 10).ToString());

        var ordre = int.TryParse(ordreSaisi, out var valeur) ? valeur : (Tailles.Count + 1) * 10;

        await ExecuterAsync(
            async () =>
            {
                await _produits
                    .EnregistrerTailleAsync(null, nom, ordre, systemeTailleId: SystemeTailleChoisi.Id)
                    .ConfigureAwait(true);
                await ChargerReferencesAsync().ConfigureAwait(true);
            },
            "Taille ajoutée avec succès.",
            "création d'une taille").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task BasculerEtatTailleAsync()
    {
        if (TailleSelectionnee is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une taille.", "Aucune sélection");
            return;
        }

        var identifiant = TailleSelectionnee.Id;
        var activer = !TailleSelectionnee.Actif;

        await ExecuterAsync(
            async () =>
            {
                await _produits.DefinirEtatTailleAsync(identifiant, activer).ConfigureAwait(true);
                await ChargerReferencesAsync().ConfigureAwait(true);
            },
            activer ? "Taille réactivée." : "Taille désactivée.",
            "changement d'état d'une taille").ConfigureAwait(true);
    }

    // ==================================================================
    // Couleurs
    // ==================================================================

    [RelayCommand]
    private async Task NouvelleCouleurAsync()
    {
        var nom = Dialogue.DemanderTexte("Nom de la couleur :", "Nouvelle couleur");

        if (string.IsNullOrWhiteSpace(nom))
        {
            return;
        }

        var code = Dialogue.DemanderTexte(
            "Code hexadécimal de la couleur (par exemple #000000) :",
            "Nouvelle couleur",
            "#000000");

        await ExecuterAsync(
            async () =>
            {
                await _produits.EnregistrerCouleurAsync(null, nom, code).ConfigureAwait(true);
                await ChargerReferencesAsync().ConfigureAwait(true);
            },
            "Couleur ajoutée avec succès.",
            "création d'une couleur").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task BasculerEtatCouleurAsync()
    {
        if (CouleurSelectionnee is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une couleur.", "Aucune sélection");
            return;
        }

        var identifiant = CouleurSelectionnee.Id;
        var activer = !CouleurSelectionnee.Actif;

        await ExecuterAsync(
            async () =>
            {
                await _produits.DefinirEtatCouleurAsync(identifiant, activer).ConfigureAwait(true);
                await ChargerReferencesAsync().ConfigureAwait(true);
            },
            activer ? "Couleur réactivée." : "Couleur désactivée.",
            "changement d'état d'une couleur").ConfigureAwait(true);
    }

    private static void Remplir<T>(ObservableCollection<T> destination, IReadOnlyList<T> source)
    {
        destination.Clear();

        foreach (var element in source)
        {
            destination.Add(element);
        }
    }

    // ==================================================================
    // Logo du magasin
    // ==================================================================

    /// <summary>
    /// Enseigne affichée sur l'écran de connexion et dans le menu. L'écran
    /// s'y lie directement : changer le logo met les deux à jour aussitôt,
    /// sans redémarrer le logiciel.
    /// </summary>
    public Views.Enseigne Enseigne => Views.Enseigne.Courante;

    /// <summary>
    /// Installe l'image choisie comme logo du magasin.
    ///
    /// Le fichier est recopié dans le dossier de données du poste, et non à
    /// côté du programme : ce dernier est parfois protégé en écriture par
    /// Windows, et le magasin ne pourrait alors plus changer son logo.
    /// </summary>
    [RelayCommand]
    private void ChoisirLogo()
    {
        var chemin = Dialogue.DemanderCheminOuverture(
            "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Tous les fichiers (*.*)|*.*");

        if (chemin is null)
        {
            return;
        }

        try
        {
            if (!Enseigne.Installer(chemin))
            {
                Dialogue.Erreur(
                    "Cette image n'a pas pu être lue. Choisissez un fichier PNG ou JPEG.",
                    "Logo non installé");

                return;
            }
        }
        catch (Exception erreur)
        {
            Journal.LogError(erreur, "Installation du logo depuis {Chemin}", chemin);

            Dialogue.Erreur(
                "Le logo n'a pas pu être enregistré. Vérifiez que le fichier " +
                "n'est pas ouvert dans un autre programme.",
                "Logo non installé");

            return;
        }

        Dialogue.Succes(
            "Logo installé. Il apparaît dès maintenant dans le menu et sur " +
            "l'écran de connexion.");
    }

    /// <summary>Revient à l'emblème dessiné dans le logiciel.</summary>
    [RelayCommand]
    private void RetirerLogo()
    {
        if (!Dialogue.Confirmer(
                "Retirer le logo du magasin ? L'emblème dessiné dans le logiciel " +
                "sera affiché à la place.",
                "Retirer le logo"))
        {
            return;
        }

        try
        {
            Enseigne.Retirer();
        }
        catch (Exception erreur)
        {
            Journal.LogError(erreur, "Retrait du logo");

            Dialogue.Erreur("Le logo n'a pas pu être retiré.", "Opération impossible");

            return;
        }

        Dialogue.Succes("Logo retiré.");
    }

    // ==================================================================
    // Sauvegarde des données du magasin
    // ==================================================================

    [ObservableProperty]
    private string _messageSauvegarde = string.Empty;

    /// <summary>
    /// Écrit une copie complète des données du magasin dans un fichier.
    /// </summary>
    [RelayCommand]
    private async Task SauvegarderAsync()
    {
        if (_sauvegarde is null)
        {
            return;
        }

        var chemin = Dialogue.DemanderCheminEnregistrement(
            ServiceSauvegarde.NomFichierPropose(),
            "Sauvegarde du magasin (*.sauvegarde)|*.sauvegarde");

        if (chemin is null)
        {
            return;
        }

        await ExecuterAsync(async () =>
        {
            var resultat = await _sauvegarde.SauvegarderAsync(chemin).ConfigureAwait(true);

            if (!resultat.Reussie)
            {
                throw new Domain.Exceptions.ExceptionMetier(resultat.Message);
            }

            MessageSauvegarde =
                $"Sauvegarde du {DateTime.Now:dd/MM/yyyy à HH:mm} enregistrée dans « {chemin} ».";

            Dialogue.Succes(
                resultat.Message + Environment.NewLine + Environment.NewLine +
                "Conservez ce fichier ailleurs que sur cet ordinateur : clé USB, " +
                "disque externe ou espace en ligne.");
        }, contexteJournal: "sauvegarde des données").ConfigureAwait(true);
    }

    /// <summary>
    /// Remplace les données actuelles par celles d'une sauvegarde. Opération
    /// irréversible, doublement confirmée.
    /// </summary>
    [RelayCommand]
    private async Task RestaurerAsync()
    {
        if (_sauvegarde is null)
        {
            return;
        }

        var chemin = Dialogue.DemanderCheminOuverture(
            "Sauvegarde du magasin (*.sauvegarde)|*.sauvegarde|Tous les fichiers (*.*)|*.*");

        if (chemin is null)
        {
            return;
        }

        if (!Dialogue.Confirmer(
                "Restaurer cette sauvegarde remplacera TOUTES les données actuelles du magasin." +
                Environment.NewLine + Environment.NewLine +
                "Les ventes, produits et clients enregistrés depuis cette sauvegarde " +
                "seront définitivement perdus." + Environment.NewLine + Environment.NewLine +
                "Voulez-vous continuer ?",
                "Restaurer une sauvegarde"))
        {
            return;
        }

        // Seconde confirmation : l'opération est irréversible et efface le
        // travail réalisé depuis la sauvegarde.
        if (!Dialogue.Confirmer(
                "Dernière vérification : les données actuelles vont être écrasées." +
                Environment.NewLine + Environment.NewLine +
                $"Fichier : {chemin}",
                "Confirmer la restauration"))
        {
            return;
        }

        await ExecuterAsync(async () =>
        {
            var resultat = await _sauvegarde.RestaurerAsync(chemin).ConfigureAwait(true);

            if (!resultat.Reussie)
            {
                throw new Domain.Exceptions.ExceptionMetier(resultat.Message);
            }

            MessageSauvegarde = $"Données restaurées depuis « {chemin} ».";

            Dialogue.Succes(
                "Données restaurées." + Environment.NewLine + Environment.NewLine +
                "Fermez puis rouvrez le logiciel pour que tous les écrans " +
                "affichent les données restaurées.");
        }, contexteJournal: "restauration d'une sauvegarde").ConfigureAwait(true);
    }
}
