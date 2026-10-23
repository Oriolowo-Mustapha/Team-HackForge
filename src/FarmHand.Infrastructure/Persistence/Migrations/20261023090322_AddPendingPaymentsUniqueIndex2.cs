using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmHand.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingPaymentsUniqueIndex2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_VetRequestId_Status",
                table: "Payments",
                columns: new[] { "VetRequestId", "Status" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_VetRequestId_Status",
                table: "Payments");
        }
    }
}
