using GestionMagasin.Domain.Interfaces;

namespace GestionMagasin.Infrastructure.Services;

/// <summary>Horloge réelle du système.</summary>
public class FournisseurHorodatage : IFournisseurHorodatage
{
    public DateTime MaintenantUtc => DateTime.UtcNow;

    public DateTime MaintenantLocal => DateTime.Now;
}
