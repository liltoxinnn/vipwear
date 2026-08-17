using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GestionMagasin.App.Services;

/// <summary>
/// Lit et enregistre la chaîne de connexion saisie par la personne qui
/// installe le logiciel.
///
/// Le fichier « appsettings.Local.json » est écrit à côté de l'exécutable et
/// surcharge « appsettings.json ». Le magasin n'a donc jamais à modifier le
/// fichier livré, et une mise à jour du logiciel ne fait pas perdre sa
/// configuration.
/// </summary>
public static class ConfigurationConnexion
{
    private const string NomFichierLocal = "appsettings.Local.json";
    private const string CleConnexion = "BaseDonnees";

    /// <summary>Chemin du fichier de configuration propre à ce poste.</summary>
    public static string CheminFichierLocal =>
        Path.Combine(AppContext.BaseDirectory, NomFichierLocal);

    /// <summary>Enregistre la chaîne de connexion pour les prochains démarrages.</summary>
    public static void Enregistrer(string chaineConnexion)
    {
        var racine = LireFichierExistant();

        if (racine["ConnectionStrings"] is not JsonObject connexions)
        {
            connexions = new JsonObject();
            racine["ConnectionStrings"] = connexions;
        }

        connexions[CleConnexion] = chaineConnexion;

        var options = new JsonSerializerOptions { WriteIndented = true };

        File.WriteAllText(CheminFichierLocal, racine.ToJsonString(options));
    }

    /// <summary>
    /// Relit le fichier existant pour ne pas écraser d'éventuels autres
    /// réglages déjà présents.
    /// </summary>
    private static JsonObject LireFichierExistant()
    {
        if (!File.Exists(CheminFichierLocal))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(CheminFichierLocal)) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            // Fichier abîmé : il sera simplement remplacé.
            return new JsonObject();
        }
    }
}
