using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIdToWifi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RestorantWifiInformations_TenantId",
                table: "RestorantWifiInformations");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "RestorantWifiInformations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestorantWifiInformations_BranchId",
                table: "RestorantWifiInformations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RestorantWifiInformations_TenantId_BranchId",
                table: "RestorantWifiInformations",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.AddForeignKey(
                name: "FK_RestorantWifiInformations_Branches_BranchId",
                table: "RestorantWifiInformations",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RestorantWifiInformations_Branches_BranchId",
                table: "RestorantWifiInformations");

            migrationBuilder.DropIndex(
                name: "IX_RestorantWifiInformations_BranchId",
                table: "RestorantWifiInformations");

            migrationBuilder.DropIndex(
                name: "IX_RestorantWifiInformations_TenantId_BranchId",
                table: "RestorantWifiInformations");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "RestorantWifiInformations");

            migrationBuilder.CreateIndex(
                name: "IX_RestorantWifiInformations_TenantId",
                table: "RestorantWifiInformations",
                column: "TenantId");
        }
    }
}
