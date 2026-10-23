using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmHand.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentIdempotencyAndAIDiagnosisUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AIDiagnoses_ObservationId",
                table: "AIDiagnoses");

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "Payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Payments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VetSubaccountUsed",
                table: "Payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_VetRequestId_IdempotencyKey",
                table: "Payments",
                columns: new[] { "VetRequestId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "AIDiagnoses"
                        GROUP BY "ObservationId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot add unique AIDiagnoses.ObservationId index: duplicate diagnoses exist. Resolve duplicates before applying this migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AIDiagnoses_ObservationId",
                table: "AIDiagnoses",
                column: "ObservationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_VetRequestId_IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_AIDiagnoses_ObservationId",
                table: "AIDiagnoses");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VetSubaccountUsed",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_AIDiagnoses_ObservationId",
                table: "AIDiagnoses",
                column: "ObservationId");
        }
    }
}
