using System.IO;
using System.Runtime.Versioning;
using System.Windows.Media.Imaging;
using GestionMagasin.App.Views;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Impression;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.App.Impression;

/// <summary>Impression des tickets de caisse sur l'imprimante thermique du poste.</summary>
public interface IServiceImpressionTicket
{
    /// <summary>Réglages actuels du poste.</summary>
    ReglagesTicket Reglages { get; }

    /// <summary>Enregistre les réglages du poste.</summary>
    void Enregistrer(ReglagesTicket reglages);

    /// <summary>Imprimantes installées dans Windows.</summary>
    IReadOnlyList<string> ListerImprimantes();

    /// <summary>Imprime le ticket d'une vente.</summary>
    /// <param name="vente">Vente enregistrée.</param>
    /// <param name="montantRecu">Somme remise par le client, si on la connaît encore.</param>
    Task ImprimerAsync(VenteDto vente, decimal? montantRecu = null);

    /// <summary>Imprime un ticket d'essai avec les réglages donnés, enregistrés ou non.</summary>
    Task ImprimerEssaiAsync(ReglagesTicket reglages);
}

/// <summary>
/// Chaîne complète d'un ticket : composition, dessin, conversion en points,
/// commandes, envoi.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ServiceImpressionTicket : IServiceImpressionTicket
{
    /// <summary>Fichier des réglages du poste, à côté du logo installé.</summary>
    public static string CheminReglages { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GestionMagasin",
        "imprimante-tickets.json");

    private readonly IServiceParametres _parametres;
    private readonly ILogger<ServiceImpressionTicket> _journal;

    public ServiceImpressionTicket(IServiceParametres parametres, ILogger<ServiceImpressionTicket> journal)
    {
        _parametres = parametres;
        _journal = journal;
    }

    public ReglagesTicket Reglages => Lire();

    public void Enregistrer(ReglagesTicket reglages)
    {
        var normalises = reglages.Normaliser();

        Directory.CreateDirectory(Path.GetDirectoryName(CheminReglages)!);

        // Écriture dans un fichier voisin puis remplacement : une coupure de
        // courant au mauvais moment laisse l'ancien fichier intact, au lieu
        // d'un fichier à moitié écrit.
        var provisoire = CheminReglages + ".tmp";
        File.WriteAllText(provisoire, normalises.VersJson());
        File.Move(provisoire, CheminReglages, overwrite: true);

        _journal.LogInformation(
            "Imprimante de tickets : « {Imprimante} », rouleau de {Largeur} mm.",
            normalises.NomImprimante ?? "aucune", (int)normalises.Largeur);
    }

    public IReadOnlyList<string> ListerImprimantes() => SpouleurWindows.ListerImprimantes();

    public async Task ImprimerAsync(VenteDto vente, decimal? montantRecu = null)
    {
        var reglages = Lire();

        if (!reglages.EstConfiguree)
        {
            throw new ImpressionTicketException(
                "Aucune imprimante de tickets n'est choisie sur ce poste. " +
                "Choisissez-la dans Paramètres, onglet Imprimante de tickets.");
        }

        await ImprimerAsync(vente, reglages, montantRecu, mention: null).ConfigureAwait(true);
    }

    public async Task ImprimerEssaiAsync(ReglagesTicket reglages)
    {
        if (!reglages.EstConfiguree)
        {
            throw new ImpressionTicketException("Choisissez d'abord l'imprimante à essayer.");
        }

        // Le ticket d'essai passe par la même composition qu'un vrai : ce qui
        // sort est exactement ce que verra le client. Les accents y figurent
        // exprès, pour vérifier qu'ils s'impriment.
        var essai = new VenteDto
        {
            NumeroVente = "ESSAI",
            DateVente = DateTime.Now,
            UtilisateurNom = "Essai d'impression",
            SousTotal = 9_400m,
            Remise = 0m,
            Total = 9_400m,
            MontantPaye = 9_400m,
            Lignes =
            [
                new LigneVenteDto
                {
                    Designation = "Chemise Oxford — Blanc / M", Quantite = 1, PrixUnitaire = 2_500m, Total = 2_500m
                },
                new LigneVenteDto
                {
                    Designation = "Veste légère été — Écru / L", Quantite = 2, PrixUnitaire = 3_650m,
                    Remise = 400m, Total = 6_900m
                }
            ],
            Paiements =
            [
                new PaiementDto
                {
                    ModePaiement = ModePaiement.Especes, LibelleModePaiement = "Espèces", Montant = 9_400m
                }
            ]
        };

        await ImprimerAsync(essai, reglages.Normaliser(), montantRecu: 10_000m, mention: "TICKET D'ESSAI")
            .ConfigureAwait(true);
    }

    private async Task ImprimerAsync(VenteDto vente, ReglagesTicket reglages, decimal? montantRecu, string? mention)
    {
        var magasin = await _parametres.ObtenirAsync().ConfigureAwait(true);

        var lignes = CompositionTicket.Composer(vente, magasin, reglages, montantRecu, mention);

        BitmapSource? logo = reglages.ImprimerLogo ? Enseigne.Courante.Logo : null;

        // Le dessin a lieu sur le fil de l'interface, seul autorisé à se
        // servir des objets graphiques de WPF ; la suite n'en a plus besoin.
        var image = RenduTicket.Dessiner(lignes, reglages, logo);

        var nomImprimante = reglages.NomImprimante!;
        var nomDocument = $"Ticket {vente.NumeroVente}";

        _journal.LogInformation(
            "Ticket {Numero} : {Lignes} lignes, {Hauteur} rangées, vers « {Imprimante} ».",
            vente.NumeroVente, lignes.Count, image.Hauteur, nomImprimante);

        await Task.Run(() =>
        {
            var points = Monochrome.VersPoints(image.Gris, image.Largeur, image.Hauteur, image.RangeesLogo);
            var travail = EscPos.Ticket(points, image.Largeur, image.Hauteur, reglages);

            SpouleurWindows.EnvoyerBrut(nomImprimante, nomDocument, travail);
        }).ConfigureAwait(true);

        _journal.LogInformation("Ticket {Numero} : remis au spouleur.", vente.NumeroVente);
    }

    private static ReglagesTicket Lire()
    {
        try
        {
            return File.Exists(CheminReglages)
                ? ReglagesTicket.DepuisJson(File.ReadAllText(CheminReglages))
                : new ReglagesTicket();
        }
        catch (IOException)
        {
            return new ReglagesTicket();
        }
        catch (UnauthorizedAccessException)
        {
            return new ReglagesTicket();
        }
    }
}
