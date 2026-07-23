using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandingDisplaySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchNameFontSize",
                table: "TenantBrandings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultMenuView",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchNameFontSize",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "DefaultMenuView",
                table: "TenantBrandings");
        }
    }
}
