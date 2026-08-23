namespace GestionMagasin.ServeurEmbarque;

/// <summary>Paramètres du serveur embarqué.</summary>
public sealed record OptionsServeur
{
    /// <summary>
    /// Dossier des données du magasin. Placé par défaut dans le profil de
    /// l'utilisateur : contrairement à « Program Files », il est accessible en
    /// écriture sans droits d'administrateur.
    /// </summary>
    public string DossierDonnees { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GestionMagasin",
        "donnees");

    /// <summary>
    /// Port d'écoute. Volontairement différent du 5432 habituel : un magasin
    /// peut déjà avoir un PostgreSQL installé, les deux doivent cohabiter.
    /// </summary>
    public int Port { get; init; } = 5433;

    /// <summary>Nom de la base applicative, créée au premier démarrage.</summary>
    public string NomBase { get; init; } = "gestionmagasin";

    /// <summary>Compte propriétaire du serveur.</summary>
    public string Utilisateur { get; init; } = "gestionmagasin";

    /// <summary>
    /// Emplacement des binaires livrés. Null pour chercher à côté de
    /// l'exécutable, puis dans le PostgreSQL du système.
    /// </summary>
    public string? RacineBinaires { get; init; }

    /// <summary>Délai maximal accordé au serveur pour devenir disponible.</summary>
    public TimeSpan DelaiDemarrage { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Fichier journal du serveur, dans le dossier des données.</summary>
    public string CheminJournal => Path.Combine(DossierDonnees, "..", "serveur-postgres.log");
}
