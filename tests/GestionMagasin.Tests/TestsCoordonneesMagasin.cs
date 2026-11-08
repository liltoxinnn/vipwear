using System.Text;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Impression;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Infrastructure.Data;
using GestionMagasin.Infrastructure.Documents;
using GestionMagasin.Tests.Socle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Coordonnées du magasin sur une installation existante.
///
/// L'adresse, le téléphone et le lien du QR code ne sont donnés qu'aux
/// installations neuves, au premier démarrage. Le magasin, lui, tournait
/// déjà : ses tickets sortaient sans eux. La mise à jour de la base les
/// renseigne là où ils manquent, sans jamais remplacer une saisie.
/// </summary>
public class TestsCoordonneesMagasin : IAsyncLifetime
{
    private const string MigrationPrecedente = "20261108185521_LienQrCodeTicket";

    private static readonly string ModeleChaineConnexion =
        Environment.GetEnvironmentVariable("GESTIONMAGASIN_TESTS_CONNEXION")
        ?? "Host=localhost;Port=5432;Database={0};Username=postgres;Password=postgres;Include Error Detail=true";

    private readonly string _nomBase = $"coordonnees_{Guid.NewGuid():N}";

    private string ChaineConnexion => string.Format(ModeleChaineConnexion, _nomBase);

    public async Task InitializeAsync()
    {
        await using var connexion = new NpgsqlConnection(string.Format(ModeleChaineConnexion, "postgres"));
        await connexion.OpenAsync();

        await using var commande = connexion.CreateCommand();
        commande.CommandText = $"CREATE DATABASE \"{_nomBase}\"";
        await commande.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var connexion = new NpgsqlConnection(string.Format(ModeleChaineConnexion, "postgres"));
        await connexion.OpenAsync();

        await using var commande = connexion.CreateCommand();
        commande.CommandText = $"DROP DATABASE IF EXISTS \"{_nomBase}\" WITH (FORCE)";
        await commande.ExecuteNonQueryAsync();
    }

    private ContexteMagasin CreerContexte() =>
        new(new DbContextOptionsBuilder<ContexteMagasin>()
            .UseNpgsql(ChaineConnexion, o => o.MigrationsAssembly(typeof(ContexteMagasin).Assembly.FullName))
            .Options);

    private async Task<(string? Adresse, string? Telephone, string? Lien)> MettreAJourAsync(string insertion)
    {
        await using (var ancienne = CreerContexte())
        {
            await ancienne.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(MigrationPrecedente);
            await ancienne.Database.ExecuteSqlRawAsync(insertion);
        }

        await using var contexte = CreerContexte();
        await contexte.Database.MigrateAsync();

        var magasin = await contexte.ParametresMagasin.AsNoTracking().SingleAsync();

        return (magasin.Adresse, magasin.Telephone, magasin.LienQrCodeTicket);
    }

    [Fact]
    public async Task Les_coordonnees_vides_d_un_magasin_existant_sont_renseignees()
    {
        var (adresse, telephone, lien) = await MettreAJourAsync("""
            INSERT INTO parametres_magasin (nom_magasin, adresse, telephone, taux_tva, delai_retour_jours)
            VALUES ('VIP MEN’S STORE', NULL, '  ', 0, 7);
            """);

        Assert.Equal("Ighil Ouazoug, à 50 mètres à côté de la mosquée", adresse);
        Assert.Equal("0557174909", telephone);
        Assert.Equal("https://linktr.ee/VIPMENSSTORE", lien);
    }

    [Fact]
    public async Task Une_coordonnee_saisie_par_le_magasin_n_est_jamais_remplacee()
    {
        var (adresse, telephone, lien) = await MettreAJourAsync("""
            INSERT INTO parametres_magasin
                (nom_magasin, adresse, telephone, lien_qr_code_ticket, taux_tva, delai_retour_jours)
            VALUES ('VIP MEN’S STORE', 'Rue de la Liberté, Béjaïa', '0550000000', 'https://exemple.dz', 0, 7);
            """);

        Assert.Equal("Rue de la Liberté, Béjaïa", adresse);
        Assert.Equal("0550000000", telephone);
        Assert.Equal("https://exemple.dz", lien);
    }
}

/// <summary>
/// Logo du magasin en tête du ticket PDF, comme sur le ticket de
/// l'imprimante de caisse.
/// </summary>
public class TestsLogoTicketPdf : BaseDeTest
{
    private sealed class LogoFixe(byte[]? octets) : ILogoMagasin
    {
        public byte[]? Lire() => octets;
    }

    private static readonly VenteDto Vente = new()
    {
        NumeroVente = "VTE-2026-000016",
        DateVente = new DateTime(2026, 11, 8, 20, 44, 0),
        SousTotal = 1_500m,
        Total = 1_500m,
        MontantPaye = 1_500m,
        Lignes = [new LigneVenteDto { Designation = "jean — Blanc / 34", Quantite = 1, PrixUnitaire = 1_500m, Total = 1_500m }],
        Paiements = [new PaiementDto { ModePaiement = ModePaiement.Especes, LibelleModePaiement = "Espèces", Montant = 1_500m }]
    };

    private async Task<int> ImagesDuTicketAsync(byte[]? logo)
    {
        var pdf = new ServiceDocumentsPdf(Service<IServiceParametres>(), new LogoFixe(logo));

        var ticket = await pdf.GenererTicketAsync(Vente);

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(ticket, 0, 5));

        var texte = Encoding.Latin1.GetString(ticket);

        return texte.Split("/Subtype /Image").Length - 1;
    }

    [Fact]
    public async Task Le_logo_du_magasin_figure_en_tete_du_ticket_pdf()
    {
        var sansLogo = await ImagesDuTicketAsync(null);
        var avecLogo = await ImagesDuTicketAsync(CodeQr.Png("VIP MEN'S STORE"));

        Assert.Equal(sansLogo + 1, avecLogo);
    }

    [Fact]
    public async Task Un_logo_abime_n_empeche_pas_le_ticket_de_sortir()
    {
        var sansLogo = await ImagesDuTicketAsync(null);
        var logoAbime = await ImagesDuTicketAsync([0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);

        Assert.Equal(sansLogo, logoAbime);
    }
}
