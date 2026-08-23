using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Securite;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>
/// Catalogue : liste des produits à gauche, fiche détaillée et tableau des
/// déclinaisons à droite. Les variantes sont toujours présentées à l'intérieur
/// de leur produit, jamais comme des articles indépendants.
/// </summary>
public partial class VueModeleProduits : VueModeleBase
{
    private readonly IServiceProduits _produits;
    private readonly IServiceExportExcel _excel;
    private readonly ISessionUtilisateur _session;

    public VueModeleProduits(
        IServiceProduits produits,
        IServiceExportExcel excel,
        ISessionUtilisateur session,
        IServiceDialogue dialogue,
        ILogger<VueModeleProduits> journal)
        : base(dialogue, journal)
    {
        _produits = produits;
        _excel = excel;
        _session = session;
    }

    public override string Titre => "Produits";

    public override string SousTitre => "Catalogue, déclinaisons, tailles et couleurs";

    public ObservableCollection<ResumeProduitDto> Produits { get; } = [];

    /// <summary>Familles d'articles, employées comme filtre du catalogue.</summary>
    public ObservableCollection<CategorieDto> Familles { get; } = [];

    public ObservableCollection<ReferenceDto> Marques { get; } = [];

    public ObservableCollection<ReferenceDto> Tailles { get; } = [];

    public ObservableCollection<ReferenceDto> Couleurs { get; } = [];

    [ObservableProperty]
    private ResumeProduitDto? _produitSelectionne;

    [ObservableProperty]
    private ProduitDto? _fiche;

    [ObservableProperty]
    private VarianteDto? _varianteSelectionnee;

    [ObservableProperty]
    private string _recherche = string.Empty;

    [ObservableProperty]
    private bool _inclureInactifs;

    [ObservableProperty]
    private ReferenceDto? _marqueFiltre;

    [ObservableProperty]
    private CategorieDto? _familleFiltre;

    public bool PeutModifier => _session.Possede(CodesPermissions.ModifierProduits);

    public bool PeutDesactiver => _session.Possede(CodesPermissions.SupprimerProduits);

    public override async Task ChargerAsync() =>
        await ExecuterAsync(async () =>
        {
            await ChargerReferencesAsync().ConfigureAwait(true);
            await ChargerListeAsync().ConfigureAwait(true);
        }, contexteJournal: "chargement du catalogue").ConfigureAwait(true);

    private async Task ChargerReferencesAsync()
    {
        Remplir(Familles, await _produits.ListerCategoriesAsync(true).ConfigureAwait(true));
        Remplir(Marques, await _produits.ListerMarquesAsync(true).ConfigureAwait(true));
        Remplir(Tailles, await _produits.ListerTaillesAsync(true).ConfigureAwait(true));
        Remplir(Couleurs, await _produits.ListerCouleursAsync(true).ConfigureAwait(true));
    }

    private async Task ChargerListeAsync()
    {
        var resultats = await _produits.RechercherProduitsAsync(
            Recherche,
            MarqueFiltre?.Id,
            inclureInactifs: InclureInactifs,
            categorieId: FamilleFiltre?.Id).ConfigureAwait(true);

        var idPrecedent = ProduitSelectionne?.Id;

        Remplir(Produits, resultats);

        ProduitSelectionne = idPrecedent is null
            ? Produits.FirstOrDefault()
            : Produits.FirstOrDefault(p => p.Id == idPrecedent) ?? Produits.FirstOrDefault();
    }

    partial void OnProduitSelectionneChanged(ResumeProduitDto? value)
    {
        _ = ExecuterAsync(() => ChargerFicheAsync(value?.Id),
            contexteJournal: "chargement de la fiche produit");
    }

    private async Task ChargerFicheAsync(int? produitId)
    {
        if (produitId is null)
        {
            Fiche = null;
            return;
        }

        Fiche = await _produits.ObtenirProduitAsync(produitId.Value).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RechercherAsync() =>
        await ExecuterAsync(ChargerListeAsync, contexteJournal: "recherche de produits").ConfigureAwait(true);

    [RelayCommand]
    private async Task ReinitialiserFiltresAsync()
    {
        Recherche = string.Empty;
        MarqueFiltre = null;
        FamilleFiltre = null;
        InclureInactifs = false;

        await ExecuterAsync(ChargerListeAsync, contexteJournal: "réinitialisation des filtres")
            .ConfigureAwait(true);
    }

    // ==================================================================
    // Produits
    // ==================================================================

    /// <summary>Déclenché pour demander l'ouverture du formulaire produit.</summary>
    public event EventHandler<ProduitDto?>? FormulaireProduitDemande;

    /// <summary>Déclenché pour demander l'ouverture du formulaire de déclinaison.</summary>
    public event EventHandler<(ProduitDto Produit, VarianteDto? Variante)>? FormulaireVarianteDemande;

    /// <summary>Déclenché pour demander la génération groupée de déclinaisons.</summary>
    public event EventHandler<ProduitDto>? GenerationVariantesDemandee;

    [RelayCommand]
    private void NouveauProduit()
    {
        if (!PeutModifier)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation de créer un produit.",
                "Action non autorisée");

            return;
        }

        FormulaireProduitDemande?.Invoke(this, null);
    }

    [RelayCommand]
    private void ModifierProduit()
    {
        if (Fiche is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un produit dans la liste.", "Aucune sélection");
            return;
        }

        if (!PeutModifier)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation de modifier un produit.",
                "Action non autorisée");

            return;
        }

        FormulaireProduitDemande?.Invoke(this, Fiche);
    }

    [RelayCommand]
    private async Task BasculerEtatProduitAsync()
    {
        if (Fiche is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un produit dans la liste.", "Aucune sélection");
            return;
        }

        var activer = !Fiche.Actif;

        var question = activer
            ? $"Réactiver le produit « {Fiche.Nom} » et le remettre en vente ?"
            : $"Désactiver le produit « {Fiche.Nom} » ?" + Environment.NewLine +
              "Il ne sera plus proposé en caisse, mais tout son historique de ventes sera conservé.";

        if (!Dialogue.Confirmer(question, activer ? "Réactiver le produit" : "Désactiver le produit"))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _produits.DefinirEtatProduitAsync(Fiche.Id, activer).ConfigureAwait(true);
                await ChargerListeAsync().ConfigureAwait(true);
                await ChargerFicheAsync(Fiche.Id).ConfigureAwait(true);
            },
            activer ? "Produit réactivé avec succès." : "Produit désactivé avec succès.",
            "changement d'état d'un produit").ConfigureAwait(true);
    }

    // ==================================================================
    // Déclinaisons
    // ==================================================================

    [RelayCommand]
    private void NouvelleVariante()
    {
        if (Fiche is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un produit dans la liste.", "Aucune sélection");
            return;
        }

        if (!PeutModifier)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation de modifier le catalogue.",
                "Action non autorisée");

            return;
        }

        FormulaireVarianteDemande?.Invoke(this, (Fiche, null));
    }

    [RelayCommand]
    private void ModifierVariante()
    {
        if (Fiche is null || VarianteSelectionnee is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une déclinaison.", "Aucune sélection");
            return;
        }

        if (!PeutModifier)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation de modifier le catalogue.",
                "Action non autorisée");

            return;
        }

        FormulaireVarianteDemande?.Invoke(this, (Fiche, VarianteSelectionnee));
    }

    [RelayCommand]
    private void GenererVariantes()
    {
        if (Fiche is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord un produit dans la liste.", "Aucune sélection");
            return;
        }

        if (!PeutModifier)
        {
            Dialogue.Avertir(
                "Vous n'avez pas l'autorisation de modifier le catalogue.",
                "Action non autorisée");

            return;
        }

        GenerationVariantesDemandee?.Invoke(this, Fiche);
    }

    [RelayCommand]
    private async Task BasculerEtatVarianteAsync()
    {
        if (VarianteSelectionnee is null)
        {
            Dialogue.Avertir("Sélectionnez d'abord une déclinaison.", "Aucune sélection");
            return;
        }

        var activer = !VarianteSelectionnee.Actif;
        var identifiant = VarianteSelectionnee.Sku;

        if (!Dialogue.Confirmer(
                activer
                    ? $"Réactiver la déclinaison « {identifiant} » ?"
                    : $"Désactiver la déclinaison « {identifiant} » ? Elle ne sera plus vendable.",
                "Confirmation"))
        {
            return;
        }

        await ExecuterAsync(
            async () =>
            {
                await _produits.DefinirEtatVarianteAsync(VarianteSelectionnee.Id, activer).ConfigureAwait(true);
                await ChargerFicheAsync(Fiche?.Id).ConfigureAwait(true);
                await ChargerListeAsync().ConfigureAwait(true);
            },
            activer ? "Déclinaison réactivée." : "Déclinaison désactivée.",
            "changement d'état d'une déclinaison").ConfigureAwait(true);
    }

    /// <summary>Rafraîchit la liste et la fiche après une création ou une modification.</summary>
    public async Task RechargerAsync(int? produitId = null) =>
        await ExecuterAsync(async () =>
        {
            await ChargerReferencesAsync().ConfigureAwait(true);
            await ChargerListeAsync().ConfigureAwait(true);

            if (produitId.HasValue)
            {
                ProduitSelectionne = Produits.FirstOrDefault(p => p.Id == produitId.Value)
                                     ?? ProduitSelectionne;

                await ChargerFicheAsync(produitId.Value).ConfigureAwait(true);
            }
        }, contexteJournal: "rechargement du catalogue").ConfigureAwait(true);

    // ==================================================================
    // Export
    // ==================================================================

    [RelayCommand]
    private async Task ExporterAsync() =>
        await ExecuterAsync(async () =>
        {
            var fiches = new List<ProduitDto>();

            foreach (var resume in Produits)
            {
                var fiche = await _produits.ObtenirProduitAsync(resume.Id).ConfigureAwait(true);

                if (fiche is not null)
                {
                    fiches.Add(fiche);
                }
            }

            var contenu = await _excel.ExporterProduitsAsync(fiches).ConfigureAwait(true);

            var chemin = Dialogue.DemanderCheminEnregistrement(
                $"Produits-{DateTime.Now:yyyy-MM-dd}.xlsx",
                "Classeur Excel (*.xlsx)|*.xlsx");

            if (chemin is null)
            {
                return;
            }

            await File.WriteAllBytesAsync(chemin, contenu).ConfigureAwait(true);

            VueModeleCaisse.OuvrirDocument(chemin);
        }, contexteJournal: "export du catalogue").ConfigureAwait(true);

    private static void Remplir<T>(ObservableCollection<T> destination, IReadOnlyList<T> source)
    {
        destination.Clear();

        foreach (var element in source)
        {
            destination.Add(element);
        }
    }
}
