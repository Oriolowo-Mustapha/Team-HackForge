using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmHand.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVetPayoutAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentSubaccountId",
                table: "VetPartners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountName",
                table: "VetPartners",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountNumber",
                table: "VetPartners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankCode",
                table: "VetPartners",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutCountry",
                table: "VetPartners",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentSubaccountId",
                table: "VetPartners");

            migrationBuilder.DropColumn(
                name: "PayoutAccountName",
                table: "VetPartners");

            migrationBuilder.DropColumn(
                name: "PayoutAccountNumber",
                table: "VetPartners");

            migrationBuilder.DropColumn(
                name: "PayoutBankCode",
                table: "VetPartners");

            migrationBuilder.DropColumn(
                name: "PayoutCountry",
                table: "VetPartners");
        }
    }
}
