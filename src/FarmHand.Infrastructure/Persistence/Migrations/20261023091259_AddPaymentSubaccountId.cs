using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmHand.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSubaccountId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubaccountId",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubaccountId",
                table: "Payments");
        }
    }
}
