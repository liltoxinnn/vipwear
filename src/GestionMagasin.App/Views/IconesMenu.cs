namespace GestionMagasin.App.Views;

/// <summary>
/// Tracés des icônes du menu.
///
/// Elles sont dessinées ici plutôt que prises dans une police de
/// pictogrammes : une police absente du poste afficherait des carrés vides,
/// et le magasin n'aurait plus aucun repère visuel. Un tracé s'affiche
/// partout, prend la couleur du texte et reste net à toutes les tailles.
///
/// Chaque icône est dessinée dans un carré de 24 sur 24.
/// </summary>
internal static class IconesMenu
{
    /// <summary>Quatre panneaux : la vue d'ensemble du magasin.</summary>
    public const string TableauDeBord =
        "M3,3 H10 V11 H3 Z  M14,3 H21 V8 H14 Z  M14,12 H21 V21 H14 Z  M3,15 H10 V21 H3 Z";

    /// <summary>Un tiroir-caisse, avec son écran et sa fente.</summary>
    public const string Caisse =
        "M3,11 H21 V20 H3 Z  M6,5 H18 V11 H6 Z  M9,8 H15  M6,15.5 H10";

    /// <summary>Un vêtement : c'est ce que vend le magasin.</summary>
    public const string Produits =
        "M8,3 L4.5,4.5 L3,8 L6,9.5 V21 H18 V9.5 L21,8 L19.5,4.5 L16,3 " +
        "C16,5.2 14.2,6.5 12,6.5 C9.8,6.5 8,5.2 8,3 Z";

    /// <summary>Des cartons empilés en réserve.</summary>
    public const string Stock =
        "M4,9 H20 V20 H4 Z  M4,9 L6.5,4 H17.5 L20,9  M12,9 V20  M9.5,4 L9.5,9  M14.5,4 L14.5,9";

    /// <summary>Un ticket de caisse au bord déchiré.</summary>
    public const string Ventes =
        "M6,3 H18 V21 L15.5,19.3 L12,21 L8.5,19.3 L6,21 Z  M9,8 H15  M9,12 H15  M9,16 H13";

    /// <summary>Un chariot : les commandes passées aux fournisseurs.</summary>
    public const string Achats =
        "M3,4 H5.5 L7.5,15 H18.5 L20.5,7 H6.5  M7.6,17.4 h2.2 v2.2 h-2.2 z  " +
        "M16,17.4 h2.2 v2.2 h-2.2 z";

    /// <summary>Un camion de livraison.</summary>
    public const string Fournisseurs =
        "M3,7 H14 V17 H3 Z  M14,10 H17.5 L21,13.5 V17 H14 Z  " +
        "M5.6,17.4 h2.4 v2.2 h-2.4 z  M15.8,17.4 h2.4 v2.2 h-2.4 z";

    /// <summary>Une personne : le fichier clients.</summary>
    public const string Clients =
        "M8,7.5 A4,4 0 1,1 16,7.5 A4,4 0 1,1 8,7.5  " +
        "M4,21 C4,16.6 7.6,14 12,14 C16.4,14 20,16.6 20,21";

    /// <summary>Une flèche qui revient : l'article rapporté par le client.</summary>
    public const string Retours =
        "M9,6 L4,11 L9,16  M4,11 H14 A5.5,5.5 0 0,1 19.5,16.5 V19";

    /// <summary>Des barres de statistiques.</summary>
    public const string Rapports =
        "M3,21 H21  M6,21 V12  M10.5,21 V5  M15,21 V15  M19.5,21 V9";

    /// <summary>Deux personnes : les comptes des employés.</summary>
    public const string Utilisateurs =
        "M6,8 A3.2,3.2 0 1,1 12.4,8 A3.2,3.2 0 1,1 6,8  " +
        "M2.5,20.5 C2.5,16.9 5.5,14.6 9.2,14.6 C12.9,14.6 15.9,16.9 15.9,20.5  " +
        "M16.2,6.2 A2.7,2.7 0 1,1 16.2,11.6  M18,14.9 C20.6,15.7 22,17.9 22,20.5";

    /// <summary>Un réglage : l'axe et ses graduations.</summary>
    public const string Parametres =
        "M12,9 A3,3 0 1,1 12,15 A3,3 0 1,1 12,9  " +
        "M12,2 V4.6  M12,19.4 V22  M2,12 H4.6  M19.4,12 H22  " +
        "M4.9,4.9 L6.8,6.8  M17.2,17.2 L19.1,19.1  " +
        "M19.1,4.9 L17.2,6.8  M6.8,17.2 L4.9,19.1";
}
