using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class color_and_platform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptAnonymus",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAndroid",
                table: "LoyaltyCards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsIOS",
                table: "LoyaltyCards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BronzeColor",
                table: "CardTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GoldColor",
                table: "CardTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlatinumColor",
                table: "CardTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SilverColor",
                table: "CardTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptAnonymus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsAndroid",
                table: "LoyaltyCards");

            migrationBuilder.DropColumn(
                name: "IsIOS",
                table: "LoyaltyCards");

            migrationBuilder.DropColumn(
                name: "BronzeColor",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "GoldColor",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "PlatinumColor",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "SilverColor",
                table: "CardTemplates");
        }
    }
}
