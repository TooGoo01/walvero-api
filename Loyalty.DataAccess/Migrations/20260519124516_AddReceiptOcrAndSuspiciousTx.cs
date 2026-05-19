using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptOcrAndSuspiciousTx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuspicious",
                table: "Transactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OcrExtractedAmount",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptImageUrl",
                table: "Transactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspiciousReason",
                table: "Transactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlertWhatsappPhone",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableSuspiciousAlert",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OcrToleranceAzn",
                table: "Tenants",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireReceiptOcr",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SuspiciousAmountCap",
                table: "Tenants",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSuspicious",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "OcrExtractedAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReceiptImageUrl",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SuspiciousReason",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AlertWhatsappPhone",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EnableSuspiciousAlert",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OcrToleranceAzn",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RequireReceiptOcr",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SuspiciousAmountCap",
                table: "Tenants");
        }
    }
}
