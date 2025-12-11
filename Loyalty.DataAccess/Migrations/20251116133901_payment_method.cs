using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class payment_method : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BronzePercentCash",
                table: "CardTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoldPercentCash",
                table: "CardTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PayMentType",
                table: "CardTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PlatinumPercentCash",
                table: "CardTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SilverPercentCash",
                table: "CardTemplates",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BronzePercentCash",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "GoldPercentCash",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "PayMentType",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "PlatinumPercentCash",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "SilverPercentCash",
                table: "CardTemplates");
        }
    }
}
