namespace GestionMagasin.Domain.Enums;

/// <summary>Mode de règlement utilisé pour un paiement.</summary>
public enum ModePaiement
{
    Especes = 1,
    CarteBancaire = 2,
    Virement = 3,
    Autre = 4
}
