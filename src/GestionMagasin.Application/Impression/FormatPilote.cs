using System.Globalization;

namespace GestionMagasin.Application.Impression;

/// <summary>
/// Accord entre la taille des étiquettes et le papier que le pilote de
/// l'imprimante utilisera réellement.
///
/// Le pilote a le dernier mot : beaucoup ignorent une taille qu'ils ne
/// connaissent pas et gardent la leur. L'imprimante, qui centre le rouleau
/// sous sa tête, pose alors l'image de travers — décalée sur le côté, et à
/// cheval sur deux étiquettes.
/// </summary>
public static class FormatPilote
{
    /// <summary>Écart toléré : les pilotes arrondissent leurs formats.</summary>
    public const double ToleranceMm = 1.0;

    /// <summary>Vrai quand le papier du pilote a la taille des étiquettes, au millimètre près.</summary>
    public static bool Convient(double largeurMm, double hauteurMm, ReglagesEtiquettes reglages)
    {
        ArgumentNullException.ThrowIfNull(reglages);

        return Math.Abs(largeurMm - reglages.LargeurMm) <= ToleranceMm
               && Math.Abs(hauteurMm - reglages.HauteurMm) <= ToleranceMm;
    }

    /// <summary>Explication du désaccord, avec la marche à suivre dans Windows.</summary>
    public static string Expliquer(string nomImprimante, ReglagesEtiquettes reglages, double largeurMm, double hauteurMm)
    {
        ArgumentNullException.ThrowIfNull(reglages);

        static string Mm(double valeur) => Math.Round(valeur).ToString("0", CultureInfo.InvariantCulture);

        var etiquette = $"{reglages.LargeurMm} × {reglages.HauteurMm} mm";

        return
            $"Le pilote de « {nomImprimante} » imprime sur du {Mm(largeurMm)} × {Mm(hauteurMm)} mm, et non sur " +
            $"vos étiquettes de {etiquette} : elles sortiraient décalées sur le côté, et à cheval sur deux " +
            "étiquettes." +
            Environment.NewLine + Environment.NewLine +
            "Pour corriger, dans Windows : Paramètres → Bluetooth et appareils → Imprimantes et scanners → " +
            $"« {nomImprimante} » → Options d'impression. Choisissez le format {etiquette} — ou créez-le " +
            "(souvent dans « Mise en page », « Papier » ou « Stock », bouton Nouveau) —, et le type de papier " +
            "« étiquettes avec espace ». Enregistrez, puis rouvrez cette fenêtre.";
    }
}
