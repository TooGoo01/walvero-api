using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIntegrationCardTrackMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CardTrackDigits",
                table: "TenantIntegrations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CardTrackMode",
                table: "TenantIntegrations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_Tenant_BarcodeValue",
                table: "LoyaltyCards",
                columns: new[] { "TenantId", "BarcodeValue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_Tenant_BarcodeValue",
                table: "LoyaltyCards");

            migrationBuilder.DropColumn(
                name: "CardTrackDigits",
                table: "TenantIntegrations");

            migrationBuilder.DropColumn(
                name: "CardTrackMode",
                table: "TenantIntegrations");
        }
    }
}
