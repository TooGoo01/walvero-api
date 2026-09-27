using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementToTenantBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnouncementAz",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnouncementEn",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AnnouncementFontSize",
                table: "TenantBrandings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnouncementRu",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncementAz",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "AnnouncementEn",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "AnnouncementFontSize",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "AnnouncementRu",
                table: "TenantBrandings");
        }
    }
}
