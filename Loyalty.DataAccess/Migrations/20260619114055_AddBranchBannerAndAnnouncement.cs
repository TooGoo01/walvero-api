using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchBannerAndAnnouncement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnouncementAz",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnouncementEn",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnouncementRu",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BannerImagesJson",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BannerVideoFileName",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BannerVideoPosterUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BannerVideoUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncementAz",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "AnnouncementEn",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "AnnouncementRu",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "BannerImagesJson",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "BannerVideoFileName",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "BannerVideoPosterUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "BannerVideoUrl",
                table: "Branches");
        }
    }
}
