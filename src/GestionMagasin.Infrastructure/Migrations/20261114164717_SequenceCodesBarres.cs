using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionMagasin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SequenceCodesBarres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "sequence_code_barres");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "sequence_code_barres");
        }
    }
}
