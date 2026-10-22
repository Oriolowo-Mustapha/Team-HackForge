using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmHand.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityAndResourceModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Flock_FlockId",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_PoultryObservations_LivestockAnimals_LivestockAnimalId",
                table: "PoultryObservations");

            migrationBuilder.DropTable(
                name: "Flock");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_FlockId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "FlockId",
                table: "Incidents");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "VetPartners",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "VetPartners",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "VetPartners"
                SET "Email" = lower(replace("Name", ' ', '.')) || '+' || "Id"::text || '@farmhand.local'
                WHERE "Email" = '';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "LivestockAnimalId",
                table: "PoultryObservations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FarmId",
                table: "PoultryObservations",
                type: "uuid",
                nullable: true);

            // Preserve existing observations by deriving their farm from the linked animal,
            // then from the linked category, before enforcing the FK.
            migrationBuilder.Sql("""
                UPDATE "PoultryObservations" AS o
                SET "FarmId" = a."FarmId"
                FROM "LivestockAnimals" AS a
                WHERE o."LivestockAnimalId" = a."Id" AND o."FarmId" IS NULL;

                UPDATE "PoultryObservations" AS o
                SET "FarmId" = c."FarmId"
                FROM "LivestockCategories" AS c
                WHERE o."LivestockCategoryId" = c."Id" AND o."FarmId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "FarmId",
                table: "PoultryObservations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LivestockAnimalId",
                table: "Incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LivestockCategoryId",
                table: "Incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VetPartners_Email",
                table: "VetPartners",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VetPartners_UserId",
                table: "VetPartners",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoultryObservations_FarmId",
                table: "PoultryObservations",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_LivestockAnimalId",
                table: "Incidents",
                column: "LivestockAnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_LivestockCategoryId",
                table: "Incidents",
                column: "LivestockCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_LivestockAnimals_LivestockAnimalId",
                table: "Incidents",
                column: "LivestockAnimalId",
                principalTable: "LivestockAnimals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_LivestockCategories_LivestockCategoryId",
                table: "Incidents",
                column: "LivestockCategoryId",
                principalTable: "LivestockCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PoultryObservations_Farms_FarmId",
                table: "PoultryObservations",
                column: "FarmId",
                principalTable: "Farms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PoultryObservations_LivestockAnimals_LivestockAnimalId",
                table: "PoultryObservations",
                column: "LivestockAnimalId",
                principalTable: "LivestockAnimals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_VetPartners_Users_UserId",
                table: "VetPartners",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_LivestockAnimals_LivestockAnimalId",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_LivestockCategories_LivestockCategoryId",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_PoultryObservations_Farms_FarmId",
                table: "PoultryObservations");

            migrationBuilder.DropForeignKey(
                name: "FK_PoultryObservations_LivestockAnimals_LivestockAnimalId",
                table: "PoultryObservations");

            migrationBuilder.DropForeignKey(
                name: "FK_VetPartners_Users_UserId",
                table: "VetPartners");

            migrationBuilder.DropIndex(
                name: "IX_VetPartners_Email",
                table: "VetPartners");

            migrationBuilder.DropIndex(
                name: "IX_VetPartners_UserId",
                table: "VetPartners");

            migrationBuilder.DropIndex(
                name: "IX_PoultryObservations_FarmId",
                table: "PoultryObservations");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_LivestockAnimalId",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_LivestockCategoryId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "VetPartners");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "VetPartners");

            migrationBuilder.DropColumn(
                name: "FarmId",
                table: "PoultryObservations");

            migrationBuilder.DropColumn(
                name: "LivestockAnimalId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "LivestockCategoryId",
                table: "Incidents");

            migrationBuilder.AlterColumn<Guid>(
                name: "LivestockAnimalId",
                table: "PoultryObservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FlockId",
                table: "Incidents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Flock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BirdType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentRecordedPopulation = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlockName = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flock", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_FlockId",
                table: "Incidents",
                column: "FlockId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Flock_FlockId",
                table: "Incidents",
                column: "FlockId",
                principalTable: "Flock",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PoultryObservations_LivestockAnimals_LivestockAnimalId",
                table: "PoultryObservations",
                column: "LivestockAnimalId",
                principalTable: "LivestockAnimals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
