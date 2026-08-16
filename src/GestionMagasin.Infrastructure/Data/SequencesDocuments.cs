namespace GestionMagasin.Infrastructure.Data;

/// <summary>Noms des séquences PostgreSQL utilisées pour numéroter les documents.</summary>
public static class SequencesDocuments
{
    public const string Ventes = "sequence_numero_vente";
    public const string Achats = "sequence_numero_achat";
    public const string Retours = "sequence_numero_retour";
}
