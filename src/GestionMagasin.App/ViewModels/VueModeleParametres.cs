using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Converters;
using GestionMagasin.App.Services;
using GestionMagasin.Application.DTOs;
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
        IServiceDialogue dialogue,
        ILogger<VueModeleParametres> journal,
        ServiceSauvegarde? sauvegarde = null)
        : base(dialogue, journal)
    {
        _parametres = parametres;
        _produits = produits;
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
            RegistreCommerce = magasin.RegistreCommerce ?? string.Empty;
            NumeroIdentificationFiscale = magasin.NumeroIdentificationFiscale ?? string.Empty;
            ArticleImposition = magasin.ArticleImposition ?? string.Empty;
            TauxTva = magasin.TauxTva;
            DelaiRetourJours = magasin.DelaiRetourJours;

            await ChargerReferencesAsync().ConfigureAwait(true);
        }, contexteJournal: "chargement des paramètres").ConfigureAwait(true);

    private async Task ChargerReferencesAsync()
    {
        Remplir(Marques, await _produits.ListerMarquesAsync(true).ConfigureAwait(true));
        Remplir(Tailles, await _produits.ListerTaillesAsync(true).ConfigureAwait(true));
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

    // ==================================================================
    // Tailles
    // ==================================================================

    [RelayCommand]
    private async Task NouvelleTailleAsync()
    {
        var nom = Dialogue.DemanderTexte(
            "Nom de la taille (XS, S, M, 38, 40…) :",
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
                await _produits.EnregistrerTailleAsync(null, nom, ordre).ConfigureAwait(true);
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
