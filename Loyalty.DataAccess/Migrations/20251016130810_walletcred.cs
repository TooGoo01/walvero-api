using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class walletcred : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppleCertFileName",
                table: "WalletCredentials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppleWwdrCertUrl",
                table: "WalletCredentials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppleWwdrFileName",
                table: "WalletCredentials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassKitAuthToken",
                table: "WalletCredentials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassKitServiceUrl",
                table: "WalletCredentials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppleAuthToken",
                table: "LoyaltyCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplePassUrl",
                table: "LoyaltyCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppleSerial",
                table: "LoyaltyCards",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppleCertFileName",
                table: "WalletCredentials");

            migrationBuilder.DropColumn(
                name: "AppleWwdrCertUrl",
                table: "WalletCredentials");

            migrationBuilder.DropColumn(
                name: "AppleWwdrFileName",
                table: "WalletCredentials");

            migrationBuilder.DropColumn(
                name: "PassKitAuthToken",
                table: "WalletCredentials");

            migrationBuilder.DropColumn(
                name: "PassKitServiceUrl",
                table: "WalletCredentials");

            migrationBuilder.DropColumn(
                name: "AppleAuthToken",
                table: "LoyaltyCards");

            migrationBuilder.DropColumn(
                name: "ApplePassUrl",
                table: "LoyaltyCards");

            migrationBuilder.DropColumn(
                name: "AppleSerial",
                table: "LoyaltyCards");
        }
    }
}
