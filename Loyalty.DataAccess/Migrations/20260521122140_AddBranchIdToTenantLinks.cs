using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIdToTenantLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuCategories_Branches_BranchId",
                table: "MenuCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Branches_BranchId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Tenants_TenantId",
                table: "MenuItems");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "TenantLinks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantLinks_BranchId",
                table: "TenantLinks",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantLinks_TenantId_BranchId_SortOrder",
                table: "TenantLinks",
                columns: new[] { "TenantId", "BranchId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_MenuCategories_Branches_BranchId",
                table: "MenuCategories",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Branches_BranchId",
                table: "MenuItems",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Tenants_TenantId",
                table: "MenuItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantLinks_Branches_BranchId",
                table: "TenantLinks",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuCategories_Branches_BranchId",
                table: "MenuCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Branches_BranchId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Tenants_TenantId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantLinks_Branches_BranchId",
                table: "TenantLinks");

            migrationBuilder.DropIndex(
                name: "IX_TenantLinks_BranchId",
                table: "TenantLinks");

            migrationBuilder.DropIndex(
                name: "IX_TenantLinks_TenantId_BranchId_SortOrder",
                table: "TenantLinks");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "TenantLinks");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuCategories_Branches_BranchId",
                table: "MenuCategories",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Branches_BranchId",
                table: "MenuItems",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Tenants_TenantId",
                table: "MenuItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
