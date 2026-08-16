using System.Text;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Infrastructure.Data;

/// <summary>
/// Applique la convention de nommage PostgreSQL (minuscules et tirets bas) à
/// l'ensemble du modèle. Les noms restent ainsi lisibles et manipulables
/// directement en SQL, sans guillemets, par un administrateur de base.
/// </summary>
internal static class ConventionsNommage
{
    public static void AppliquerSnakeCase(ModelBuilder modelBuilder)
    {
        foreach (var typeEntite in modelBuilder.Model.GetEntityTypes())
        {
            var nomTable = typeEntite.GetTableName();
            if (nomTable is not null)
            {
                typeEntite.SetTableName(VersSnakeCase(nomTable));
            }

            foreach (var propriete in typeEntite.GetProperties())
            {
                // Les colonnes système de PostgreSQL (xmin) gardent leur nom.
                if (propriete.GetColumnName() == "xmin")
                {
                    continue;
                }

                propriete.SetColumnName(VersSnakeCase(propriete.Name));
            }

            foreach (var cle in typeEntite.GetKeys())
            {
                var nom = cle.GetName();
                if (nom is not null)
                {
                    cle.SetName(VersSnakeCase(nom));
                }
            }

            foreach (var cleEtrangere in typeEntite.GetForeignKeys())
            {
                var nom = cleEtrangere.GetConstraintName();
                if (nom is not null)
                {
                    cleEtrangere.SetConstraintName(VersSnakeCase(nom));
                }
            }

            foreach (var index in typeEntite.GetIndexes())
            {
                var nom = index.GetDatabaseName();
                if (nom is not null)
                {
                    index.SetDatabaseName(VersSnakeCase(nom));
                }
            }
        }
    }

    /// <summary>
    /// Transforme « VarianteProduitId » en « variante_produit_id ».
    /// Les sigles sont conservés d'un seul tenant : « SKU » devient « sku »
    /// et non « s_k_u », « PK_Produits » devient « pk_produits ».
    /// </summary>
    private static string VersSnakeCase(string nom)
    {
        if (string.IsNullOrEmpty(nom))
        {
            return nom;
        }

        var resultat = new StringBuilder(nom.Length + 8);

        for (var i = 0; i < nom.Length; i++)
        {
            var caractere = nom[i];

            if (char.IsUpper(caractere) && i > 0 && resultat[^1] != '_')
            {
                var precedent = nom[i - 1];
                var suivant = i + 1 < nom.Length ? nom[i + 1] : '\0';

                // Une coupure a lieu à la fin d'un mot en minuscules
                // (« ProduitId ») ou à la fin d'un sigle suivi d'un mot
                // (« SKUProduit » → « sku_produit »).
                var finDeMot = !char.IsUpper(precedent) && precedent != '_';
                var finDeSigle = char.IsUpper(precedent) && char.IsLower(suivant);

                if (finDeMot || finDeSigle)
                {
                    resultat.Append('_');
                }
            }

            resultat.Append(char.ToLowerInvariant(caractere));
        }

        return resultat.ToString();
    }
}
