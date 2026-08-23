using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GestionMagasin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CategoriesEtSystemesTailles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tailles_nom",
                table: "tailles");

            migrationBuilder.CreateTable(
                name: "systemes_tailles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ordre = table.Column<int>(type: "integer", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_systemes_tailles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    systeme_taille_id = table.Column<int>(type: "integer", nullable: false),
                    ordre = table.Column<int>(type: "integer", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_systemes_tailles_systeme_taille_id",
                        column: x => x.systeme_taille_id,
                        principalTable: "systemes_tailles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ----------------------------------------------------------
            // Reprise des données existantes.
            //
            // Les deux liens sont obligatoires, mais la base d'un magasin en
            // exploitation contient déjà des tailles et des produits. Les
            // colonnes arrivent donc facultatives, sont renseignées, puis
            // rendues obligatoires : ajoutées obligatoires d'emblée, la
            // contrainte serait violée à la première ligne et la mise à jour
            // échouerait — le magasin resterait bloqué au démarrage.
            //
            // Seul le strict nécessaire est créé ici. Le reste du catalogue
            // — pantalons, pointures, taille unique — est amorcé par le
            // logiciel juste après, où il est plus lisible et modifiable.
            // ----------------------------------------------------------

            migrationBuilder.Sql("""
                INSERT INTO systemes_tailles (nom, ordre, actif)
                VALUES ('Tailles vêtements (XS à XXXL)', 10, TRUE);
                """);

            migrationBuilder.Sql("""
                INSERT INTO categories (nom, systeme_taille_id, ordre, actif)
                SELECT 'Non classé', s.id, 999, TRUE
                  FROM systemes_tailles s
                 WHERE s.nom = 'Tailles vêtements (XS à XXXL)';
                """);

            migrationBuilder.AddColumn<int>(
                name: "systeme_taille_id",
                table: "tailles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "categorie_id",
                table: "produits",
                type: "integer",
                nullable: true);

            // Les tailles livrées à l'installation sont toutes des lettres :
            // elles rejoignent le système des vêtements. Une taille numérique
            // saisie à la main s'y retrouvera aussi, et devra être déplacée
            // depuis les Paramètres — le nom seul ne dit pas si « 42 » est une
            // pointure ou une taille de pantalon.
            migrationBuilder.Sql("""
                UPDATE tailles
                   SET systeme_taille_id = (
                        SELECT id FROM systemes_tailles
                         WHERE nom = 'Tailles vêtements (XS à XXXL)')
                 WHERE systeme_taille_id IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE produits
                   SET categorie_id = (SELECT id FROM categories WHERE nom = 'Non classé')
                 WHERE categorie_id IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "systeme_taille_id",
                table: "tailles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "categorie_id",
                table: "produits",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tailles_systeme_taille_id_nom",
                table: "tailles",
                columns: new[] { "systeme_taille_id", "nom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produits_categorie_id",
                table: "produits",
                column: "categorie_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_nom",
                table: "categories",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_ordre",
                table: "categories",
                column: "ordre");

            migrationBuilder.CreateIndex(
                name: "ix_categories_systeme_taille_id",
                table: "categories",
                column: "systeme_taille_id");

            migrationBuilder.CreateIndex(
                name: "ix_systemes_tailles_nom",
                table: "systemes_tailles",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_systemes_tailles_ordre",
                table: "systemes_tailles",
                column: "ordre");

            migrationBuilder.AddForeignKey(
                name: "fk_produits_categories_categorie_id",
                table: "produits",
                column: "categorie_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tailles_systemes_tailles_systeme_taille_id",
                table: "tailles",
                column: "systeme_taille_id",
                principalTable: "systemes_tailles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_produits_categories_categorie_id",
                table: "produits");

            migrationBuilder.DropForeignKey(
                name: "fk_tailles_systemes_tailles_systeme_taille_id",
                table: "tailles");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "systemes_tailles");

            migrationBuilder.DropIndex(
                name: "ix_tailles_systeme_taille_id_nom",
                table: "tailles");

            migrationBuilder.DropIndex(
                name: "ix_produits_categorie_id",
                table: "produits");

            migrationBuilder.DropColumn(
                name: "systeme_taille_id",
                table: "tailles");

            migrationBuilder.DropColumn(
                name: "categorie_id",
                table: "produits");

            migrationBuilder.CreateIndex(
                name: "ix_tailles_nom",
                table: "tailles",
                column: "nom",
                unique: true);
        }
    }
}
