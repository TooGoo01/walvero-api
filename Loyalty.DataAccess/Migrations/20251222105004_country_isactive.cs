using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class country_isactive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CountrySequences",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CountrySequences");
        }
    }
}
