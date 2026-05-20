using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPerBranchSuspiciousConfigAndBranchIdOnRedeemRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SuspiciousSpendCountCap",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "RedeemRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlertWhatsappPhone",
                table: "Branches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableSuspiciousAlert",
                table: "Branches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OcrToleranceAzn",
                table: "Branches",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireReceiptOcr",
                table: "Branches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SuspiciousAmountCap",
                table: "Branches",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuspiciousSpendCountCap",
                table: "Branches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RedeemRequests_BranchId",
                table: "RedeemRequests",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_RedeemRequests_Branches_BranchId",
                table: "RedeemRequests",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RedeemRequests_Branches_BranchId",
                table: "RedeemRequests");

            migrationBuilder.DropIndex(
                name: "IX_RedeemRequests_BranchId",
                table: "RedeemRequests");

            migrationBuilder.DropColumn(
                name: "SuspiciousSpendCountCap",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "RedeemRequests");

            migrationBuilder.DropColumn(
                name: "AlertWhatsappPhone",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "EnableSuspiciousAlert",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "OcrToleranceAzn",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "RequireReceiptOcr",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "SuspiciousAmountCap",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "SuspiciousSpendCountCap",
                table: "Branches");
        }
    }
}
