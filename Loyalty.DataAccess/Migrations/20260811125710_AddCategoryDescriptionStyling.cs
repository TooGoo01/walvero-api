using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryDescriptionStyling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoryDescriptionColor",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryDescriptionFontSize",
                table: "TenantBrandings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionColor",
                table: "MenuCategories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DescriptionFontSize",
                table: "MenuCategories",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryDescriptionColor",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "CategoryDescriptionFontSize",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "DescriptionColor",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "DescriptionFontSize",
                table: "MenuCategories");
        }
    }
}
