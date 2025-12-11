using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class tierpercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BronzePercent",
                table: "CardTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoldPercent",
                table: "CardTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatinumPercent",
                table: "CardTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SilverPercent",
                table: "CardTemplates",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BronzePercent",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "GoldPercent",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "PlatinumPercent",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "SilverPercent",
                table: "CardTemplates");
        }
    }
}
