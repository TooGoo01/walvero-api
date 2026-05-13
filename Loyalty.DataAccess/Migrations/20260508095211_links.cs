using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class links : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinterestUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThreadsUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TripAdvisorUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwitterUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YelpUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YoutubeUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinterestUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThreadsUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TripAdvisorUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwitterUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YelpUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YoutubeUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinterestUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ThreadsUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TripAdvisorUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TwitterUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "YelpUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "YoutubeUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PinterestUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ThreadsUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TripAdvisorUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TwitterUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "YelpUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "YoutubeUrl",
                table: "Branches");
        }
    }
}
