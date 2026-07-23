using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchDisplaySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchNameFontSize",
                table: "TenantBrandings");

            migrationBuilder.AddColumn<int>(
                name: "AnnouncementFontSize",
                table: "Branches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultMenuView",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncementFontSize",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "DefaultMenuView",
                table: "Branches");

            migrationBuilder.AddColumn<int>(
                name: "BranchNameFontSize",
                table: "TenantBrandings",
                type: "int",
                nullable: true);
        }
    }
}
