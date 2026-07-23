using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddNonFiscalReceiptSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowNonFiscalReceipts",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptBranchNames",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowNonFiscalReceipts",
                table: "Branches",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptBranchNames",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowNonFiscalReceipts",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ReceiptBranchNames",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AllowNonFiscalReceipts",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ReceiptBranchNames",
                table: "Branches");
        }
    }
}
