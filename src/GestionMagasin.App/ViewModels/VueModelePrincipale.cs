using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionMagasin.App.Services;
using GestionMagasin.Application.Common;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Securite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.ViewModels;

/// <summary>Entrée du menu latéral.</summary>
public partial class EntreeMenu : ObservableObject
{
    public required string Libelle { get; init; }

    /// <summary>
    /// Tracé de l'icône, dessiné dans le logiciel. Voir <see cref="Views.IconesMenu"/>.
    /// </summary>
    public required string Icone { get; init; }

    /// <summary>Permission requise pour voir cette entrée. Vide = toujours visible.</summary>
    public string? PermissionRequise { get; init; }

    public required Type TypeVueModele { get; init; }

    /// <summary>Raccourci clavier affiché à droite du libellé.</summary>
    public string? Raccourci { get; init; }

    [ObservableProperty]
    private bool _estActive;
}

/// <summary>
/// Vue-modèle de la fenêtre principale : menu, page affichée et informations
/// de l'utilisateur connecté.
/// </summary>
public partial class VueModelePrincipale : VueModeleBase
{
    private readonly IServiceNavigation _navigation;
    private readonly SessionUtilisateur _session;

    // La fenêtre principale vit aussi longtemps que le logiciel. Elle ne peut
    // donc pas conserver de services liés à un contexte de données : elle
    // retiendrait le même contexte toute la journée. Ses rares accès aux
    // données ouvrent une portée le temps de l'opération.
    private readonly IServiceScopeFactory _portees;

    private readonly EventHandler<VueModeleBase> _surPageChangee;

    public VueModelePrincipale(
        IServiceNavigation navigation,
        IServiceScopeFactory portees,
        SessionUtilisateur session,
        IServiceDialogue dialogue,
        ILogger<VueModelePrincipale> journal)
        : base(dialogue, journal)
    {
        _navigation = navigation;
        _portees = portees;
        _session = session;

        // La référence est conservée pour pouvoir se désabonner : le service
        // de navigation vit plus longtemps que cette vue-modèle, recréée à
        // chaque session. Sans cela, les sessions précédentes resteraient
        // branchées dessus.
        _surPageChangee = (_, page) => PageCourante = page;
        _navigation.PageChangee += _surPageChangee;

        ConstruireMenu();
    }

    public override string Titre => "VIP MEN’S STORE";

    [ObservableProperty]
    private VueModeleBase? _pageCourante;

    [ObservableProperty]
    private string _nomMagasin = "VIP MEN’S STORE";

    [ObservableProperty]
    private string _nomUtilisateur = string.Empty;

    [ObservableProperty]
    private string _roleUtilisateur = string.Empty;

    [ObservableProperty]
    private string _dateDuJour = string.Empty;

    /// <summary>Initiales du compte connecté, affichées dans la pastille du menu.</summary>
    public string InitialesUtilisateur
    {
        get
        {
            var mots = NomUtilisateur.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return mots.Length switch
            {
                0 => "?",
                1 => mots[0][..1].ToUpperInvariant(),
                _ => (mots[0][..1] + mots[^1][..1]).ToUpperInvariant()
            };
        }
    }

    partial void OnNomUtilisateurChanged(string value) =>
        OnPropertyChanged(nameof(InitialesUtilisateur));

    /// <summary>Entrées de menu visibles pour l'utilisateur connecté.</summary>
    public ObservableCollection<EntreeMenu> Menu { get; } = [];

    /// <summary>Déclenché lorsque l'utilisateur se déconnecte.</summary>
    public event EventHandler? DeconnexionDemandee;

    /// <summary>Détache la session en cours du service de navigation.</summary>
    public void Liberer() => _navigation.PageChangee -= _surPageChangee;

    public override async Task ChargerAsync()
    {
        using (var portee = _portees.CreateScope())
        {
            var parametres = portee.ServiceProvider.GetRequiredService<IServiceParametres>();
            var magasin = await parametres.ObtenirAsync().ConfigureAwait(true);

            NomMagasin = magasin.NomMagasin;
        }

        NomUtilisateur = _session.NomComplet;
        RoleUtilisateur = _session.NomRole;
        DateDuJour = DateTime.Now.ToString("dddd d MMMM yyyy", FormatageMontant.CultureApplication);

        ConstruireMenu();

        // Le caissier arrive directement en caisse, les autres profils sur le
        // tableau de bord : chacun démarre sur l'écran qui le concerne.
        if (_session.Possede(CodesPermissions.VoirRapports))
        {
            await NaviguerAsync<VueModeleTableauBord>().ConfigureAwait(true);
        }
        else if (_session.Possede(CodesPermissions.EffectuerVente))
        {
            await NaviguerAsync<VueModeleCaisse>().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Construit le menu en ne retenant que les entrées autorisées par le
    /// rôle : un écran inaccessible n'est jamais affiché.
    /// </summary>
    private void ConstruireMenu()
    {
        Menu.Clear();

        EntreeMenu[] entrees =
        [
            new() { Libelle = "Tableau de bord", Icone = Views.IconesMenu.TableauDeBord, TypeVueModele = typeof(VueModeleTableauBord),
                    PermissionRequise = CodesPermissions.VoirRapports, Raccourci = "F1" },
            new() { Libelle = "Caisse", Icone = Views.IconesMenu.Caisse, TypeVueModele = typeof(VueModeleCaisse),
                    PermissionRequise = CodesPermissions.EffectuerVente, Raccourci = "F2" },
            new() { Libelle = "Produits", Icone = Views.IconesMenu.Produits, TypeVueModele = typeof(VueModeleProduits),
                    PermissionRequise = CodesPermissions.VoirProduits, Raccourci = "F3" },
            new() { Libelle = "Stock", Icone = Views.IconesMenu.Stock, TypeVueModele = typeof(VueModeleStock),
                    PermissionRequise = CodesPermissions.VoirStock, Raccourci = "F4" },
            new() { Libelle = "Ventes", Icone = Views.IconesMenu.Ventes, TypeVueModele = typeof(VueModeleVentes),
                    PermissionRequise = CodesPermissions.EffectuerVente, Raccourci = "F5" },
            new() { Libelle = "Achats", Icone = Views.IconesMenu.Achats, TypeVueModele = typeof(VueModeleAchats),
                    PermissionRequise = CodesPermissions.VoirAchats, Raccourci = "F6" },
            new() { Libelle = "Fournisseurs", Icone = Views.IconesMenu.Fournisseurs, TypeVueModele = typeof(VueModeleFournisseurs),
                    PermissionRequise = CodesPermissions.VoirAchats, Raccourci = "F7" },
            new() { Libelle = "Clients", Icone = Views.IconesMenu.Clients, TypeVueModele = typeof(VueModeleClients),
                    PermissionRequise = CodesPermissions.VoirClients, Raccourci = "F8" },
            new() { Libelle = "Retours", Icone = Views.IconesMenu.Retours, TypeVueModele = typeof(VueModeleRetours),
                    PermissionRequise = CodesPermissions.EffectuerRetour, Raccourci = "F9" },
            new() { Libelle = "Rapports", Icone = Views.IconesMenu.Rapports, TypeVueModele = typeof(VueModeleRapports),
                    PermissionRequise = CodesPermissions.VoirRapports, Raccourci = "F10" },
            new() { Libelle = "Utilisateurs", Icone = Views.IconesMenu.Utilisateurs, TypeVueModele = typeof(VueModeleUtilisateurs),
                    PermissionRequise = CodesPermissions.GererUtilisateurs, Raccourci = "F11" },
            new() { Libelle = "Paramètres", Icone = Views.IconesMenu.Parametres, TypeVueModele = typeof(VueModeleParametres),
                    PermissionRequise = CodesPermissions.GererParametres, Raccourci = "F12" }
        ];

        foreach (var entree in entrees)
        {
            if (entree.PermissionRequise is null || _session.Possede(entree.PermissionRequise))
            {
                Menu.Add(entree);
            }
        }
    }

    [RelayCommand]
    private async Task OuvrirAsync(EntreeMenu? entree)
    {
        if (entree is null || EstOccupe)
        {
            return;
        }

        await ExecuterAsync(async () =>
        {
            await NaviguerParTypeAsync(entree.TypeVueModele).ConfigureAwait(true);
        }, contexteJournal: $"ouverture de l'écran {entree.Libelle}").ConfigureAwait(true);
    }

    /// <summary>Ouvre un écran depuis le code, en mettant le menu à jour.</summary>
    public async Task NaviguerAsync<T>() where T : VueModeleBase =>
        await NaviguerParTypeAsync(typeof(T)).ConfigureAwait(true);

    private async Task NaviguerParTypeAsync(Type typeVueModele)
    {
        foreach (var element in Menu)
        {
            element.EstActive = element.TypeVueModele == typeVueModele;
        }

        // La navigation est générique : la méthode correspondante est
        // appelée par réflexion pour éviter un long aiguillage à maintenir.
        var methode = typeof(IServiceNavigation)
            .GetMethod(nameof(IServiceNavigation.NaviguerAsync))!
            .MakeGenericMethod(typeVueModele);

        await (Task)methode.Invoke(_navigation, null)!;
    }

    [RelayCommand]
    private async Task RafraichirAsync() =>
        await ExecuterAsync(() => _navigation.RafraichirAsync(),
            contexteJournal: "rafraîchissement de l'écran").ConfigureAwait(true);

    [RelayCommand]
    private async Task SeDeconnecterAsync()
    {
        if (!Dialogue.Confirmer(
                "Êtes-vous sûr de vouloir vous déconnecter ?",
                "Déconnexion"))
        {
            return;
        }

        using (var portee = _portees.CreateScope())
        {
            var authentification = portee.ServiceProvider.GetRequiredService<IServiceAuthentification>();
            await authentification.DeconnecterAsync().ConfigureAwait(true);
        }

        DeconnexionDemandee?.Invoke(this, EventArgs.Empty);
    }
}
