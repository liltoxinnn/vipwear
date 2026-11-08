namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>Logo du magasin, pour les documents imprimés.</summary>
public interface ILogoMagasin
{
    /// <summary>
    /// Contenu du fichier du logo (PNG, JPEG ou BMP), ou rien si le magasin
    /// n'en a pas fourni.
    /// </summary>
    byte[]? Lire();
}
