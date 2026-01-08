using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class country_sequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
         

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "CountrySequences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Flag",
                table: "CountrySequences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "CountrySequences",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "CountrySequences");

            migrationBuilder.DropColumn(
                name: "Flag",
                table: "CountrySequences");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "CountrySequences");

           
        }
    }
}
