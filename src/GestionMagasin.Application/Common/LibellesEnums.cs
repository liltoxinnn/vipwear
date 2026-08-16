using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Application.Common;

/// <summary>
/// Traduit les énumérations techniques en libellés français affichables.
/// Regrouper ces textes ici garantit qu'un même état est nommé de la même
/// façon partout : écrans, tickets, rapports et exports.
/// </summary>
public static class LibellesEnums
{
    public static string Libelle(this TypeMouvementStock valeur) => valeur switch
    {
        TypeMouvementStock.Achat => "Achat",
        TypeMouvementStock.Vente => "Vente",
        TypeMouvementStock.RetourClient => "Retour client",
        TypeMouvementStock.RetourFournisseur => "Retour fournisseur",
        TypeMouvementStock.Ajustement => "Ajustement",
        TypeMouvementStock.ProduitEndommage => "Produit endommagé",
        TypeMouvementStock.Perte => "Perte",
        TypeMouvementStock.Correction => "Correction",
        _ => valeur.ToString()
    };

    public static string Libelle(this ModePaiement valeur) => valeur switch
    {
        ModePaiement.Especes => "Espèces",
        ModePaiement.CarteBancaire => "Carte bancaire",
        ModePaiement.Virement => "Virement",
        ModePaiement.Autre => "Autre",
        _ => valeur.ToString()
    };

    public static string Libelle(this StatutVente valeur) => valeur switch
    {
        StatutVente.PartiellementPayee => "Partiellement payée",
        StatutVente.Payee => "Payée",
        StatutVente.PartiellementRetournee => "Partiellement retournée",
        StatutVente.Retournee => "Retournée",
        StatutVente.Annulee => "Annulée",
        _ => valeur.ToString()
    };

    public static string Libelle(this StatutAchat valeur) => valeur switch
    {
        StatutAchat.EnAttente => "En attente de réception",
        StatutAchat.PartiellementRecu => "Partiellement reçu",
        StatutAchat.Recu => "Reçu",
        StatutAchat.Annule => "Annulé",
        _ => valeur.ToString()
    };

    public static string Libelle(this StatutRetour valeur) => valeur switch
    {
        StatutRetour.Valide => "Validé",
        StatutRetour.Refuse => "Refusé",
        _ => valeur.ToString()
    };

    public static string Libelle(this EtatArticleRetour valeur) => valeur switch
    {
        EtatArticleRetour.Revendable => "Revendable",
        EtatArticleRetour.Endommage => "Endommagé",
        _ => valeur.ToString()
    };

    /// <summary>Liste (valeur, libellé) destinée à alimenter les listes déroulantes.</summary>
    public static IReadOnlyList<(T Valeur, string Libelle)> Choix<T>(Func<T, string> traducteur)
        where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => (v, traducteur(v))).ToList();
}
