using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestionMagasin.Infrastructure.Documents;

/// <summary>
/// Charte graphique commune à tous les documents imprimés : mêmes couleurs,
/// mêmes tailles, mêmes marges. Les tickets et les factures d'un même magasin
/// se ressemblent ainsi toujours.
/// </summary>
internal static class StyleDocuments
{
    public static readonly Color CouleurPrincipale = Color.FromHex("#1F3864");
    public static readonly Color CouleurSecondaire = Color.FromHex("#4472C4");
    public static readonly Color CouleurTexte = Color.FromHex("#1A1A1A");
    public static readonly Color CouleurTexteAttenue = Color.FromHex("#5A5A5A");
    public static readonly Color CouleurBordure = Color.FromHex("#D0D0D0");
    public static readonly Color CouleurFondEntete = Color.FromHex("#EEF2F9");
    public static readonly Color CouleurFondAlterne = Color.FromHex("#F7F9FC");
    public static readonly Color CouleurAlerte = Color.FromHex("#C0392B");
    public static readonly Color CouleurSucces = Color.FromHex("#1E7E34");

    public const float TaillePolice = 9.5f;
    public const float TaillePoliceTicket = 8.5f;
    public const float TaillePoliceTitre = 18f;
    public const float TaillePoliceSousTitre = 11f;

    /// <summary>Format d'un rouleau de caisse de 80 mm de large.</summary>
    public static PageSize FormatTicket { get; } = new(80, 297, Unit.Millimetre);
}
