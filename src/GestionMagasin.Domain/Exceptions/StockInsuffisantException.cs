namespace GestionMagasin.Domain.Exceptions;

/// <summary>
/// Levée lorsqu'une opération demande plus d'articles que le stock n'en
/// contient. Elle interrompt la transaction en cours : aucune vente ne peut
/// être enregistrée sans stock suffisant.
/// </summary>
public class StockInsuffisantException : ExceptionMetier
{
    public StockInsuffisantException(string designation, int demande, int disponible)
        : base($"Stock insuffisant pour cet article. « {designation} » : {demande} demandé(s), {disponible} disponible(s).")
    {
        Designation = designation;
        QuantiteDemandee = demande;
        QuantiteDisponible = disponible;
    }

    public string Designation { get; }

    public int QuantiteDemandee { get; }

    public int QuantiteDisponible { get; }
}
