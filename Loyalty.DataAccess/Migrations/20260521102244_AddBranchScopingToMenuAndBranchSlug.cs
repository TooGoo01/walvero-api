using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchScopingToMenuAndBranchSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuCategories_TenantId",
                table: "MenuCategories");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "MenuItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MenuItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "MenuCategories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Branches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // Backfill: mövcud MenuItems-də TenantId-i kateqoriyadan al
            migrationBuilder.Sql(@"
                UPDATE mi
                SET mi.TenantId = mc.TenantId
                FROM MenuItems mi
                INNER JOIN MenuCategories mc ON mc.Id = mi.MenuCategoryId
                WHERE mi.TenantId IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_BranchId",
                table: "MenuItems",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_TenantId_BranchId_MenuCategoryId",
                table: "MenuItems",
                columns: new[] { "TenantId", "BranchId", "MenuCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_BranchId",
                table: "MenuCategories",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_TenantId_BranchId",
                table: "MenuCategories",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId_Slug",
                table: "Branches",
                columns: new[] { "TenantId", "Slug" },
                unique: true,
                filter: "[Slug] IS NOT NULL");

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

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_BranchId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_TenantId_BranchId_MenuCategoryId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuCategories_BranchId",
                table: "MenuCategories");

            migrationBuilder.DropIndex(
                name: "IX_MenuCategories_TenantId_BranchId",
                table: "MenuCategories");

            migrationBuilder.DropIndex(
                name: "IX_Branches_TenantId_Slug",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Branches");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_TenantId",
                table: "MenuCategories",
                column: "TenantId");
        }
    }
}
