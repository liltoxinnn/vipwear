using Npgsql;

namespace GestionMagasin.Infrastructure.Services;

/// <summary>Résultat d'un test de connexion, avec un message destiné à l'utilisateur.</summary>
public record ResultatConnexionBase(bool Reussie, string Message);

/// <summary>
/// Vérifie l'accès au serveur PostgreSQL avant le démarrage du logiciel et
/// crée la base du magasin si elle n'existe pas encore.
///
/// Les messages sont rédigés pour une personne qui installe le logiciel dans
/// son magasin, pas pour un développeur : ils indiquent quoi vérifier plutôt
/// que de recopier l'erreur technique.
/// </summary>
public static class TesteurConnexion
{
    /// <summary>Compose une chaîne de connexion à partir des champs du formulaire.</summary>
    public static string ComposerChaine(
        string serveur,
        int port,
        string baseDonnees,
        string utilisateur,
        string motDePasse) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = serveur.Trim(),
            Port = port,
            Database = baseDonnees.Trim(),
            Username = utilisateur.Trim(),
            Password = motDePasse,
            Timeout = 10,
            CommandTimeout = 60
        }.ConnectionString;

    /// <summary>
    /// Teste la connexion et crée la base si nécessaire. Le serveur doit être
    /// joignable et le compte doit avoir le droit de créer une base.
    /// </summary>
    public static async Task<ResultatConnexionBase> VerifierEtPreparerAsync(
        string chaineConnexion,
        CancellationToken jeton = default)
    {
        NpgsqlConnectionStringBuilder constructeur;

        try
        {
            constructeur = new NpgsqlConnectionStringBuilder(chaineConnexion);
        }
        catch (Exception)
        {
            return new ResultatConnexionBase(false,
                "Les informations de connexion saisies ne sont pas valides.");
        }

        var nomBase = constructeur.Database;

        if (string.IsNullOrWhiteSpace(nomBase))
        {
            return new ResultatConnexionBase(false, "Le nom de la base de données est obligatoire.");
        }

        // La connexion est d'abord tentée sur la base du magasin. Si elle
        // n'existe pas encore, on se rabat sur la base d'administration pour
        // la créer.
        try
        {
            await using var connexion = new NpgsqlConnection(chaineConnexion);
            await connexion.OpenAsync(jeton).ConfigureAwait(false);

            return new ResultatConnexionBase(true,
                $"Connexion réussie à la base « {nomBase} ».");
        }
        catch (PostgresException erreur) when (erreur.SqlState == "3D000")
        {
            // 3D000 : la base n'existe pas. C'est le cas normal à la première
            // installation.
            return await CreerBaseAsync(constructeur, nomBase, jeton).ConfigureAwait(false);
        }
        catch (PostgresException erreur) when (erreur.SqlState is "28P01" or "28000")
        {
            return new ResultatConnexionBase(false,
                "Le nom d'utilisateur ou le mot de passe du serveur de base de données est incorrect.");
        }
        catch (NpgsqlException)
        {
            return new ResultatConnexionBase(false,
                $"Le serveur « {constructeur.Host}:{constructeur.Port} » ne répond pas." +
                Environment.NewLine +
                "Vérifiez que PostgreSQL est installé et démarré, puis réessayez.");
        }
        catch (Exception)
        {
            return new ResultatConnexionBase(false,
                "La connexion au serveur de base de données a échoué.");
        }
    }

    private static async Task<ResultatConnexionBase> CreerBaseAsync(
        NpgsqlConnectionStringBuilder constructeur,
        string nomBase,
        CancellationToken jeton)
    {
        var administration = new NpgsqlConnectionStringBuilder(constructeur.ConnectionString)
        {
            Database = "postgres"
        };

        try
        {
            await using var connexion = new NpgsqlConnection(administration.ConnectionString);
            await connexion.OpenAsync(jeton).ConfigureAwait(false);

            // Le nom provient du formulaire : il est inséré entre guillemets
            // doubles, guillemets internes échappés, pour qu'il ne puisse pas
            // être interprété comme une instruction.
            var nomEchappe = nomBase.Replace("\"", "\"\"");

            await using var commande = connexion.CreateCommand();
            commande.CommandText = $"CREATE DATABASE \"{nomEchappe}\"";

            await commande.ExecuteNonQueryAsync(jeton).ConfigureAwait(false);

            return new ResultatConnexionBase(true,
                $"La base « {nomBase} » a été créée. Le logiciel va préparer les tables.");
        }
        catch (PostgresException erreur) when (erreur.SqlState == "42501")
        {
            return new ResultatConnexionBase(false,
                $"La base « {nomBase} » n'existe pas et le compte « {constructeur.Username} » " +
                "n'a pas le droit de la créer." + Environment.NewLine +
                "Demandez à votre administrateur de créer la base, ou utilisez un compte disposant de ce droit.");
        }
        catch (Exception)
        {
            return new ResultatConnexionBase(false,
                $"La base « {nomBase} » n'existe pas et n'a pas pu être créée automatiquement.");
        }
    }
}
