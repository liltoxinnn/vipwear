using GestionMagasin.Application.Impression;

namespace GestionMagasin.App.Services;

/// <summary>Suite donnée au choix de l'imprimante de tickets.</summary>
public enum SuiteChoixImprimante
{
    /// <summary>Une imprimante est choisie : elle est retenue et le ticket y part.</summary>
    Imprimer,

    /// <summary>Ce ticket-ci est produit en PDF, et rien n'est retenu.</summary>
    Pdf,

    /// <summary>Pas de ticket.</summary>
    Annuler
}

/// <summary>Imprimante de tickets choisie, avec la largeur de son rouleau.</summary>
public sealed record ChoixImprimanteTicket(
    SuiteChoixImprimante Suite,
    string? Nom = null,
    LargeurRouleau Largeur = LargeurRouleau.Rouleau58);

/// <summary>
/// Affichage des messages destinés à l'utilisateur. Passer par ce service
/// plutôt que par MessageBox permet aux vues-modèles de rester testables et
/// garantit que tous les messages suivent la même présentation, en français.
/// </summary>
public interface IServiceDialogue
{
    /// <summary>Message d'information après une opération réussie.</summary>
    void Informer(string message, string titre = "Information");

    /// <summary>Message de succès, par exemple « Opération effectuée avec succès. »</summary>
    void Succes(string message);

    /// <summary>Avertissement n'empêchant pas de poursuivre.</summary>
    void Avertir(string message, string titre = "Attention");

    /// <summary>Message d'erreur.</summary>
    void Erreur(string message, string titre = "Erreur");

    /// <summary>Demande une confirmation avant une opération importante.</summary>
    bool Confirmer(string message, string titre = "Confirmation");

    /// <summary>Demande la saisie d'un texte court (motif, quantité...).</summary>
    string? DemanderTexte(string message, string titre, string valeurInitiale = "");

    /// <summary>Propose l'enregistrement d'un fichier et retourne le chemin choisi.</summary>
    string? DemanderCheminEnregistrement(string nomParDefaut, string filtre);

    /// <summary>Propose la sélection d'un fichier existant.</summary>
    string? DemanderCheminOuverture(string filtre);

    /// <summary>
    /// Demande sur quelle imprimante sortent les tickets, l'imprimante de
    /// caisse présélectionnée si l'une s'annonce comme telle.
    /// </summary>
    /// <param name="imprimantes">Imprimantes proposées.</param>
    /// <param name="avertissement">Pourquoi la question est posée de nouveau, s'il y a lieu.</param>
    ChoixImprimanteTicket ChoisirImprimanteTicket(IReadOnlyList<string> imprimantes, string? avertissement = null);
}
