using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionMagasin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoordonneesMagasin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----------------------------------------------------------
            // Coordonnées du magasin imprimées sur les tickets.
            //
            // Une installation neuve les reçoit du logiciel au premier
            // démarrage. Une installation existante, elle, avait été créée
            // avant : l'adresse, le téléphone et le lien du QR code y étaient
            // vides, et ses tickets sortaient sans eux.
            //
            // Seuls les champs vides sont renseignés : ce que le magasin a
            // saisi lui-même dans Paramètres n'est jamais remplacé.
            // ----------------------------------------------------------

            migrationBuilder.Sql("""
                UPDATE parametres_magasin
                   SET adresse = 'Ighil Ouazoug, à 50 mètres à côté de la mosquée'
                 WHERE adresse IS NULL OR btrim(adresse) = '';
                """);

            migrationBuilder.Sql("""
                UPDATE parametres_magasin
                   SET telephone = '0557174909'
                 WHERE telephone IS NULL OR btrim(telephone) = '';
                """);

            migrationBuilder.Sql("""
                UPDATE parametres_magasin
                   SET lien_qr_code_ticket = 'https://linktr.ee/VIPMENSSTORE'
                 WHERE lien_qr_code_ticket IS NULL OR btrim(lien_qr_code_ticket) = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rien à défaire : on ne sait plus quels champs étaient vides, et
            // effacer les coordonnées du magasin ferait perdre une saisie.
        }
    }
}
