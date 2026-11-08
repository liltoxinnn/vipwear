using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionMagasin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LienQrCodeTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lien_qr_code_ticket",
                table: "parametres_magasin",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lien_qr_code_ticket",
                table: "parametres_magasin");
        }
    }
}
