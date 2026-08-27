using CommunityToolkit.Mvvm.ComponentModel;

namespace GestionMagasin.App.ViewModels;

/// <summary>
/// Onglet de famille au-dessus du rayon de la caisse.
///
/// « Tout le rayon » n'est pas une famille : c'est l'absence de filtre. Le
/// distinguer par un drapeau plutôt que par un nom réservé évite qu'un
/// magasin qui créerait une famille du même nom fasse disparaître son propre
/// catalogue.
/// </summary>
public sealed partial class FamilleRayon : ObservableObject
{
    /// <summary>Onglet sans filtre, toujours en tête.</summary>
    public static FamilleRayon ToutLeRayon { get; } = new();

    private FamilleRayon()
    {
        Nom = "Tout le rayon";
        TouteFamille = true;
    }

    public FamilleRayon(string nom)
    {
        Nom = nom;
        TouteFamille = false;
    }

    public string Nom { get; }

    /// <summary>Vrai pour l'onglet qui n'applique aucun filtre.</summary>
    public bool TouteFamille { get; }

    /// <summary>
    /// Vrai pour l'onglet retenu. L'état vit sur l'onglet lui-même : les
    /// boutons sont fabriqués par une liste, et un groupe de boutons radio
    /// ne saurait pas lequel cocher au rechargement du rayon.
    /// </summary>
    [ObservableProperty]
    private bool _estActive;
}
