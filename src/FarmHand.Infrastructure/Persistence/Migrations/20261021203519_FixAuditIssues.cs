using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmHand.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixAuditIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TreatmentPlans_VetRequestId",
                table: "TreatmentPlans");

            migrationBuilder.DropIndex(
                name: "IX_PreventionPlans_IncidentId",
                table: "PreventionPlans");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "PoultryObservations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlans_VetRequestId",
                table: "TreatmentPlans",
                column: "VetRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreventionPlans_IncidentId",
                table: "PreventionPlans",
                column: "IncidentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoultryObservations_IdempotencyKey",
                table: "PoultryObservations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransactionReference",
                table: "Payments",
                column: "TransactionReference",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CostShares_VetRequests_VetRequestId",
                table: "CostShares",
                column: "VetRequestId",
                principalTable: "VetRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Flocks_Farms_FarmId",
                table: "Flocks",
                column: "FarmId",
                principalTable: "Farms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Incidents_IncidentId",
                table: "FollowUps",
                column: "IncidentId",
                principalTable: "Incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentActions_Incidents_IncidentId",
                table: "IncidentActions",
                column: "IncidentId",
                principalTable: "Incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Farms_FarmId",
                table: "Incidents",
                column: "FarmId",
                principalTable: "Farms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Flocks_FlockId",
                table: "Incidents",
                column: "FlockId",
                principalTable: "Flocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LivestockRecords_Farms_FarmId",
                table: "LivestockRecords",
                column: "FarmId",
                principalTable: "Farms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_VetRequests_VetRequestId",
                table: "Payments",
                column: "VetRequestId",
                principalTable: "VetRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PoultryObservations_Flocks_FlockId",
                table: "PoultryObservations",
                column: "FlockId",
                principalTable: "Flocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PreventionPlans_Incidents_IncidentId",
                table: "PreventionPlans",
                column: "IncidentId",
                principalTable: "Incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TreatmentPlans_VetRequests_VetRequestId",
                table: "TreatmentPlans",
                column: "VetRequestId",
                principalTable: "VetRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TreatmentScheduleItems_TreatmentPlans_TreatmentPlanId",
                table: "TreatmentScheduleItems",
                column: "TreatmentPlanId",
                principalTable: "TreatmentPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VetRequests_Incidents_IncidentId",
                table: "VetRequests",
                column: "IncidentId",
                principalTable: "Incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CostShares_VetRequests_VetRequestId",
                table: "CostShares");

            migrationBuilder.DropForeignKey(
                name: "FK_Flocks_Farms_FarmId",
                table: "Flocks");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Incidents_IncidentId",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_IncidentActions_Incidents_IncidentId",
                table: "IncidentActions");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Farms_FarmId",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Flocks_FlockId",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_LivestockRecords_Farms_FarmId",
                table: "LivestockRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_VetRequests_VetRequestId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PoultryObservations_Flocks_FlockId",
                table: "PoultryObservations");

            migrationBuilder.DropForeignKey(
                name: "FK_PreventionPlans_Incidents_IncidentId",
                table: "PreventionPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_TreatmentPlans_VetRequests_VetRequestId",
                table: "TreatmentPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_TreatmentScheduleItems_TreatmentPlans_TreatmentPlanId",
                table: "TreatmentScheduleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_VetRequests_Incidents_IncidentId",
                table: "VetRequests");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentPlans_VetRequestId",
                table: "TreatmentPlans");

            migrationBuilder.DropIndex(
                name: "IX_PreventionPlans_IncidentId",
                table: "PreventionPlans");

            migrationBuilder.DropIndex(
                name: "IX_PoultryObservations_IdempotencyKey",
                table: "PoultryObservations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TransactionReference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "PoultryObservations");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlans_VetRequestId",
                table: "TreatmentPlans",
                column: "VetRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventionPlans_IncidentId",
                table: "PreventionPlans",
                column: "IncidentId");
        }
    }
}
