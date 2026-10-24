using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmHand.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActiveVetRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve every historical case. Existing duplicates need an operator's review;
            // migration must never silently cancel, reassign, or delete veterinary work.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT "IncidentId" FROM "VetRequests"
                        WHERE "Status" IN (0, 1) AND "IsDeleted" = FALSE
                        GROUP BY "IncidentId" HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Multiple active vet requests exist for a case. Review duplicate IncidentId groups before retrying the migration; no records were changed.';
                    END IF;
                END $$;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_VetRequests_OneActivePerIncident",
                table: "VetRequests",
                columns: new[] { "IncidentId", "IsDeleted" },
                unique: true,
                filter: "\"Status\" IN (0, 1) AND \"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VetRequests_OneActivePerIncident",
                table: "VetRequests");
        }
    }
}
