using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmHand.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLivestockHistoryAndAIDiagnosis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformCommissionAmount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PlatformCommissionCurrency",
                table: "Payments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmountAmount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TotalAmountCurrency",
                table: "Payments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "VetPayoutAmountAmount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VetPayoutAmountCurrency",
                table: "Payments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DiseaseHistory",
                table: "LivestockAnimals",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLactationDate",
                table: "LivestockAnimals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVeterinaryVisit",
                table: "LivestockAnimals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Parentage",
                table: "LivestockAnimals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecentGestationDate",
                table: "LivestockAnimals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vaccinations",
                table: "LivestockAnimals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AIDiagnoses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    DiagnosisSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RecommendedAction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequiresVetConsultation = table.Column<bool>(type: "boolean", nullable: false),
                    IncidentCreated = table.Column<bool>(type: "boolean", nullable: false),
                    RawAssessment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIDiagnoses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByToken = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIDiagnoses_IncidentId",
                table: "AIDiagnoses",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_AIDiagnoses_ObservationId",
                table: "AIDiagnoses",
                column: "ObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_AIDiagnoses_Severity",
                table: "AIDiagnoses",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_RevokedAt",
                table: "RefreshTokens",
                column: "RevokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIDiagnoses");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlatformCommissionAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PlatformCommissionCurrency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TotalAmountAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TotalAmountCurrency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VetPayoutAmountAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VetPayoutAmountCurrency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DiseaseHistory",
                table: "LivestockAnimals");

            migrationBuilder.DropColumn(
                name: "LastLactationDate",
                table: "LivestockAnimals");

            migrationBuilder.DropColumn(
                name: "LastVeterinaryVisit",
                table: "LivestockAnimals");

            migrationBuilder.DropColumn(
                name: "Parentage",
                table: "LivestockAnimals");

            migrationBuilder.DropColumn(
                name: "RecentGestationDate",
                table: "LivestockAnimals");

            migrationBuilder.DropColumn(
                name: "Vaccinations",
                table: "LivestockAnimals");
        }
    }
}
