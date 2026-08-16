using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Informations du magasin utilisées dans l'interface et sur tous les
/// documents imprimés (tickets, factures, rapports). Une seule ligne existe.
/// </summary>
public class ParametresMagasin : EntiteBase
{
    public string NomMagasin { get; set; } = "Mon Magasin";

    /// <summary>Chemin du logo imprimé en tête des documents.</summary>
    public string? Logo { get; set; }

    public string? Adresse { get; set; }

    public string? Telephone { get; set; }

    public string? Email { get; set; }

    /// <summary>Devise du magasin. Valeur par défaut : DZD (dinar algérien).</summary>
    public string Devise { get; set; } = "DZD";

    /// <summary>Symbole court affiché à côté des montants.</summary>
    public string SymboleDevise { get; set; } = "DA";

    /// <summary>Message libre imprimé en pied de ticket (remerciements, conditions...).</summary>
    public string? InformationsTicket { get; set; }

    /// <summary>Numéro de registre du commerce, mention obligatoire en Algérie.</summary>
    public string? RegistreCommerce { get; set; }

    /// <summary>Numéro d'identification fiscale.</summary>
    public string? NumeroIdentificationFiscale { get; set; }

    /// <summary>Numéro d'article d'imposition.</summary>
    public string? ArticleImposition { get; set; }

    /// <summary>Taux de TVA appliqué, en pourcentage.</summary>
    public decimal TauxTva { get; set; } = 19m;

    /// <summary>Nombre de jours pendant lesquels un retour reste accepté.</summary>
    public int DelaiRetourJours { get; set; } = 30;

    public DateTime? DateModification { get; set; }
}
