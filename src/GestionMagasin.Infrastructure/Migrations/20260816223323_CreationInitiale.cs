using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GestionMagasin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreationInitiale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "sequence_numero_achat");

            migrationBuilder.CreateSequence(
                name: "sequence_numero_retour");

            migrationBuilder.CreateSequence(
                name: "sequence_numero_vente");

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telephone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    adresse = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    date_creation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "couleurs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code_couleur = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_couleurs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fournisseurs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    entreprise = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    telephone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    adresse = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    date_creation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fournisseurs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "marques",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marques", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parametres_magasin",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom_magasin = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    logo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    adresse = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    telephone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "DZD"),
                    symbole_devise = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "DA"),
                    informations_ticket = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    registre_commerce = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    numero_identification_fiscale = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    article_imposition = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    taux_tva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    delai_retour_jours = table.Column<int>(type: "integer", nullable: false),
                    date_modification = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parametres_magasin", x => x.id);
                    table.CheckConstraint("ck_parametres_delai_retour_positif", "delai_retour_jours >= 0");
                    table.CheckConstraint("ck_parametres_ligne_unique", "id = 1");
                    table.CheckConstraint("ck_parametres_tva_valide", "taux_tva >= 0 AND taux_tva <= 100");
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nom = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    categorie = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    est_systeme = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tailles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ordre = table.Column<int>(type: "integer", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tailles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "produits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sku = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    marque_id = table.Column<int>(type: "integer", nullable: true),
                    collection = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    saison = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    prix_achat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    prix_vente = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    photo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    date_creation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_modification = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produits", x => x.id);
                    table.CheckConstraint("ck_produits_prix_achat_positif", "prix_achat >= 0");
                    table.CheckConstraint("ck_produits_prix_vente_positif", "prix_vente >= 0");
                    table.ForeignKey(
                        name: "fk_produits_marques_marque_id",
                        column: x => x.marque_id,
                        principalTable: "marques",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    permission_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "utilisateurs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    prenom = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nom_utilisateur = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    date_creation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    derniere_connexion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_utilisateurs", x => x.id);
                    table.ForeignKey(
                        name: "fk_utilisateurs_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "variantes_produits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    produit_id = table.Column<int>(type: "integer", nullable: false),
                    taille_id = table.Column<int>(type: "integer", nullable: false),
                    couleur_id = table.Column<int>(type: "integer", nullable: false),
                    sku = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    code_barres = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    prix_achat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    prix_vente = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    seuil_minimum = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variantes_produits", x => x.id);
                    table.CheckConstraint("ck_variantes_prix_achat_positif", "prix_achat IS NULL OR prix_achat >= 0");
                    table.CheckConstraint("ck_variantes_prix_vente_positif", "prix_vente IS NULL OR prix_vente >= 0");
                    table.CheckConstraint("ck_variantes_seuil_positif", "seuil_minimum >= 0");
                    table.ForeignKey(
                        name: "fk_variantes_produits_couleurs_couleur_id",
                        column: x => x.couleur_id,
                        principalTable: "couleurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variantes_produits_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_variantes_produits_tailles_taille_id",
                        column: x => x.taille_id,
                        principalTable: "tailles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "achats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fournisseur_id = table.Column<int>(type: "integer", nullable: false),
                    numero_achat = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    date_achat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_reception = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sous_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    remise = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_paye = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_restant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    utilisateur_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_achats", x => x.id);
                    table.CheckConstraint("ck_achats_montants_positifs", "sous_total >= 0 AND remise >= 0 AND total >= 0 AND montant_paye >= 0");
                    table.ForeignKey(
                        name: "fk_achats_fournisseurs_fournisseur_id",
                        column: x => x.fournisseur_id,
                        principalTable: "fournisseurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_achats_utilisateurs_utilisateur_id",
                        column: x => x.utilisateur_id,
                        principalTable: "utilisateurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journaux_audit",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    utilisateur_id = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    type_entite = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entite_id = table.Column<int>(type: "integer", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    date_creation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journaux_audit", x => x.id);
                    table.ForeignKey(
                        name: "fk_journaux_audit_utilisateurs_utilisateur_id",
                        column: x => x.utilisateur_id,
                        principalTable: "utilisateurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ventes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_vente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    client_id = table.Column<int>(type: "integer", nullable: true),
                    utilisateur_id = table.Column<int>(type: "integer", nullable: false),
                    date_vente = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sous_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    remise = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_paye = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ventes", x => x.id);
                    table.CheckConstraint("ck_ventes_montants_positifs", "sous_total >= 0 AND remise >= 0 AND total >= 0 AND montant_paye >= 0");
                    table.ForeignKey(
                        name: "fk_ventes_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ventes_utilisateurs_utilisateur_id",
                        column: x => x.utilisateur_id,
                        principalTable: "utilisateurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventaires",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    variante_produit_id = table.Column<int>(type: "integer", nullable: false),
                    quantite_disponible = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    date_modification = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventaires", x => x.id);
                    table.CheckConstraint("ck_inventaires_quantite_positive", "quantite_disponible >= 0");
                    table.ForeignKey(
                        name: "fk_inventaires_variantes_produits_variante_produit_id",
                        column: x => x.variante_produit_id,
                        principalTable: "variantes_produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mouvements_stock",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    variante_produit_id = table.Column<int>(type: "integer", nullable: false),
                    type_mouvement = table.Column<int>(type: "integer", nullable: false),
                    quantite = table.Column<int>(type: "integer", nullable: false),
                    ancienne_quantite = table.Column<int>(type: "integer", nullable: false),
                    nouvelle_quantite = table.Column<int>(type: "integer", nullable: false),
                    motif = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    reference_document = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    utilisateur_id = table.Column<int>(type: "integer", nullable: true),
                    date_creation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mouvements_stock", x => x.id);
                    table.ForeignKey(
                        name: "fk_mouvements_stock_utilisateurs_utilisateur_id",
                        column: x => x.utilisateur_id,
                        principalTable: "utilisateurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_mouvements_stock_variantes_produits_variante_produit_id",
                        column: x => x.variante_produit_id,
                        principalTable: "variantes_produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lignes_achat",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    achat_id = table.Column<int>(type: "integer", nullable: false),
                    variante_produit_id = table.Column<int>(type: "integer", nullable: false),
                    quantite = table.Column<int>(type: "integer", nullable: false),
                    quantite_recue = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    prix_unitaire = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lignes_achat", x => x.id);
                    table.CheckConstraint("ck_lignes_achat_prix_positif", "prix_unitaire >= 0");
                    table.CheckConstraint("ck_lignes_achat_quantite_positive", "quantite > 0");
                    table.CheckConstraint("ck_lignes_achat_recue_coherente", "quantite_recue >= 0 AND quantite_recue <= quantite");
                    table.ForeignKey(
                        name: "fk_lignes_achat_achats_achat_id",
                        column: x => x.achat_id,
                        principalTable: "achats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lignes_achat_variantes_produits_variante_produit_id",
                        column: x => x.variante_produit_id,
                        principalTable: "variantes_produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lignes_vente",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vente_id = table.Column<int>(type: "integer", nullable: false),
                    variante_produit_id = table.Column<int>(type: "integer", nullable: false),
                    quantite = table.Column<int>(type: "integer", nullable: false),
                    prix_unitaire = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    remise = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    prix_achat_unitaire = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantite_retournee = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lignes_vente", x => x.id);
                    table.CheckConstraint("ck_lignes_vente_prix_positif", "prix_unitaire >= 0 AND remise >= 0");
                    table.CheckConstraint("ck_lignes_vente_quantite_positive", "quantite > 0");
                    table.CheckConstraint("ck_lignes_vente_retour_coherent", "quantite_retournee >= 0 AND quantite_retournee <= quantite");
                    table.ForeignKey(
                        name: "fk_lignes_vente_variantes_produits_variante_produit_id",
                        column: x => x.variante_produit_id,
                        principalTable: "variantes_produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lignes_vente_ventes_vente_id",
                        column: x => x.vente_id,
                        principalTable: "ventes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paiements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vente_id = table.Column<int>(type: "integer", nullable: false),
                    mode_paiement = table.Column<int>(type: "integer", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    date_paiement = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paiements", x => x.id);
                    table.CheckConstraint("ck_paiements_montant_positif", "montant > 0");
                    table.ForeignKey(
                        name: "fk_paiements_ventes_vente_id",
                        column: x => x.vente_id,
                        principalTable: "ventes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "retours",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    numero_retour = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    vente_id = table.Column<int>(type: "integer", nullable: false),
                    client_id = table.Column<int>(type: "integer", nullable: true),
                    utilisateur_id = table.Column<int>(type: "integer", nullable: false),
                    date_retour = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    motif = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    total_rembourse = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    statut = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retours", x => x.id);
                    table.CheckConstraint("ck_retours_montant_positif", "total_rembourse >= 0");
                    table.ForeignKey(
                        name: "fk_retours_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_retours_utilisateurs_utilisateur_id",
                        column: x => x.utilisateur_id,
                        principalTable: "utilisateurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_retours_ventes_vente_id",
                        column: x => x.vente_id,
                        principalTable: "ventes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lignes_retour",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    retour_id = table.Column<int>(type: "integer", nullable: false),
                    ligne_vente_id = table.Column<int>(type: "integer", nullable: false),
                    variante_produit_id = table.Column<int>(type: "integer", nullable: false),
                    quantite = table.Column<int>(type: "integer", nullable: false),
                    etat_article = table.Column<int>(type: "integer", nullable: false),
                    remis_en_stock = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    montant_rembourse = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lignes_retour", x => x.id);
                    table.CheckConstraint("ck_lignes_retour_etat_coherent", "remis_en_stock = false OR etat_article = 1");
                    table.CheckConstraint("ck_lignes_retour_montant_positif", "montant_rembourse >= 0");
                    table.CheckConstraint("ck_lignes_retour_quantite_positive", "quantite > 0");
                    table.ForeignKey(
                        name: "fk_lignes_retour_lignes_vente_ligne_vente_id",
                        column: x => x.ligne_vente_id,
                        principalTable: "lignes_vente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lignes_retour_retours_retour_id",
                        column: x => x.retour_id,
                        principalTable: "retours",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lignes_retour_variantes_produits_variante_produit_id",
                        column: x => x.variante_produit_id,
                        principalTable: "variantes_produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_achats_date_achat",
                table: "achats",
                column: "date_achat");

            migrationBuilder.CreateIndex(
                name: "ix_achats_fournisseur_id",
                table: "achats",
                column: "fournisseur_id");

            migrationBuilder.CreateIndex(
                name: "ix_achats_numero_achat",
                table: "achats",
                column: "numero_achat",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_achats_statut",
                table: "achats",
                column: "statut");

            migrationBuilder.CreateIndex(
                name: "ix_achats_utilisateur_id",
                table: "achats",
                column: "utilisateur_id");

            migrationBuilder.CreateIndex(
                name: "ix_clients_actif",
                table: "clients",
                column: "actif");

            migrationBuilder.CreateIndex(
                name: "ix_clients_nom",
                table: "clients",
                column: "nom");

            migrationBuilder.CreateIndex(
                name: "ix_clients_telephone",
                table: "clients",
                column: "telephone");

            migrationBuilder.CreateIndex(
                name: "ix_couleurs_nom",
                table: "couleurs",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fournisseurs_actif",
                table: "fournisseurs",
                column: "actif");

            migrationBuilder.CreateIndex(
                name: "ix_fournisseurs_nom",
                table: "fournisseurs",
                column: "nom");

            migrationBuilder.CreateIndex(
                name: "ix_fournisseurs_telephone",
                table: "fournisseurs",
                column: "telephone");

            migrationBuilder.CreateIndex(
                name: "ux_inventaires_variante",
                table: "inventaires",
                column: "variante_produit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journaux_audit_date_creation",
                table: "journaux_audit",
                column: "date_creation");

            migrationBuilder.CreateIndex(
                name: "ix_journaux_audit_type_entite_entite_id",
                table: "journaux_audit",
                columns: new[] { "type_entite", "entite_id" });

            migrationBuilder.CreateIndex(
                name: "ix_journaux_audit_utilisateur_id",
                table: "journaux_audit",
                column: "utilisateur_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_achat_achat_id",
                table: "lignes_achat",
                column: "achat_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_achat_variante_produit_id",
                table: "lignes_achat",
                column: "variante_produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_retour_ligne_vente_id",
                table: "lignes_retour",
                column: "ligne_vente_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_retour_retour_id",
                table: "lignes_retour",
                column: "retour_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_retour_variante_produit_id",
                table: "lignes_retour",
                column: "variante_produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_vente_variante_produit_id",
                table: "lignes_vente",
                column: "variante_produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_vente_vente_id",
                table: "lignes_vente",
                column: "vente_id");

            migrationBuilder.CreateIndex(
                name: "ix_marques_nom",
                table: "marques",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_stock_date_creation",
                table: "mouvements_stock",
                column: "date_creation");

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_stock_reference_document",
                table: "mouvements_stock",
                column: "reference_document");

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_stock_type_mouvement",
                table: "mouvements_stock",
                column: "type_mouvement");

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_stock_utilisateur_id",
                table: "mouvements_stock",
                column: "utilisateur_id");

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_stock_variante_produit_id_date_creation",
                table: "mouvements_stock",
                columns: new[] { "variante_produit_id", "date_creation" });

            migrationBuilder.CreateIndex(
                name: "ix_paiements_date_paiement",
                table: "paiements",
                column: "date_paiement");

            migrationBuilder.CreateIndex(
                name: "ix_paiements_mode_paiement",
                table: "paiements",
                column: "mode_paiement");

            migrationBuilder.CreateIndex(
                name: "ix_paiements_vente_id",
                table: "paiements",
                column: "vente_id");

            migrationBuilder.CreateIndex(
                name: "ix_permissions_code",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produits_actif",
                table: "produits",
                column: "actif");

            migrationBuilder.CreateIndex(
                name: "ix_produits_marque_id",
                table: "produits",
                column: "marque_id");

            migrationBuilder.CreateIndex(
                name: "ix_produits_nom",
                table: "produits",
                column: "nom");

            migrationBuilder.CreateIndex(
                name: "ix_produits_reference",
                table: "produits",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produits_sku",
                table: "produits",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retours_client_id",
                table: "retours",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_retours_date_retour",
                table: "retours",
                column: "date_retour");

            migrationBuilder.CreateIndex(
                name: "ix_retours_numero_retour",
                table: "retours",
                column: "numero_retour",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retours_utilisateur_id",
                table: "retours",
                column: "utilisateur_id");

            migrationBuilder.CreateIndex(
                name: "ix_retours_vente_id",
                table: "retours",
                column: "vente_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_nom",
                table: "roles",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tailles_nom",
                table: "tailles",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tailles_ordre",
                table: "tailles",
                column: "ordre");

            migrationBuilder.CreateIndex(
                name: "ix_utilisateurs_nom_utilisateur",
                table: "utilisateurs",
                column: "nom_utilisateur",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_utilisateurs_role_id",
                table: "utilisateurs",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_variantes_produits_couleur_id",
                table: "variantes_produits",
                column: "couleur_id");

            migrationBuilder.CreateIndex(
                name: "ix_variantes_produits_sku",
                table: "variantes_produits",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_variantes_produits_taille_id",
                table: "variantes_produits",
                column: "taille_id");

            migrationBuilder.CreateIndex(
                name: "ux_variantes_code_barres",
                table: "variantes_produits",
                column: "code_barres",
                unique: true,
                filter: "code_barres IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_variantes_produit_taille_couleur",
                table: "variantes_produits",
                columns: new[] { "produit_id", "taille_id", "couleur_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ventes_client_id",
                table: "ventes",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventes_date_vente",
                table: "ventes",
                column: "date_vente");

            migrationBuilder.CreateIndex(
                name: "ix_ventes_numero_vente",
                table: "ventes",
                column: "numero_vente",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ventes_statut",
                table: "ventes",
                column: "statut");

            migrationBuilder.CreateIndex(
                name: "ix_ventes_utilisateur_id",
                table: "ventes",
                column: "utilisateur_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventaires");

            migrationBuilder.DropTable(
                name: "journaux_audit");

            migrationBuilder.DropTable(
                name: "lignes_achat");

            migrationBuilder.DropTable(
                name: "lignes_retour");

            migrationBuilder.DropTable(
                name: "mouvements_stock");

            migrationBuilder.DropTable(
                name: "paiements");

            migrationBuilder.DropTable(
                name: "parametres_magasin");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "achats");

            migrationBuilder.DropTable(
                name: "lignes_vente");

            migrationBuilder.DropTable(
                name: "retours");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "fournisseurs");

            migrationBuilder.DropTable(
                name: "variantes_produits");

            migrationBuilder.DropTable(
                name: "ventes");

            migrationBuilder.DropTable(
                name: "couleurs");

            migrationBuilder.DropTable(
                name: "produits");

            migrationBuilder.DropTable(
                name: "tailles");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "utilisateurs");

            migrationBuilder.DropTable(
                name: "marques");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropSequence(
                name: "sequence_numero_achat");

            migrationBuilder.DropSequence(
                name: "sequence_numero_retour");

            migrationBuilder.DropSequence(
                name: "sequence_numero_vente");
        }
    }
}
