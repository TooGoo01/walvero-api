using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <summary>
    /// Adds multi-language support (AzName, EnName, RuName) to Country and City entities
    /// </summary>
    public partial class AddMultiLanguageSupportToLocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add multi-language columns to Countries table
            migrationBuilder.AddColumn<string>(
                name: "AzName",
                table: "Countries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EnName",
                table: "Countries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RuName",
                table: "Countries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Add multi-language columns to Cities table
            migrationBuilder.AddColumn<string>(
                name: "AzName",
                table: "Cities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EnName",
                table: "Cities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RuName",
                table: "Cities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Migrate existing data: copy Name to all language fields
            migrationBuilder.Sql(@"
                UPDATE Countries 
                SET AzName = Name, EnName = Name, RuName = Name 
                WHERE AzName = '' OR AzName IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE Cities 
                SET AzName = Name, EnName = Name, RuName = Name 
                WHERE AzName = '' OR AzName IS NULL;
            ");

            // Create indexes for better search performance
            migrationBuilder.CreateIndex(
                name: "IX_Countries_AzName",
                table: "Countries",
                column: "AzName");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_AzName",
                table: "Cities",
                column: "AzName");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_countryId_AzName",
                table: "Cities",
                columns: new[] { "countryId", "AzName" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_Cities_countryId_AzName",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_Cities_AzName",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_Countries_AzName",
                table: "Countries");

            // Drop columns from Cities
            migrationBuilder.DropColumn(
                name: "RuName",
                table: "Cities");

            migrationBuilder.DropColumn(
                name: "EnName",
                table: "Cities");

            migrationBuilder.DropColumn(
                name: "AzName",
                table: "Cities");

            // Drop columns from Countries
            migrationBuilder.DropColumn(
                name: "RuName",
                table: "Countries");

            migrationBuilder.DropColumn(
                name: "EnName",
                table: "Countries");

            migrationBuilder.DropColumn(
                name: "AzName",
                table: "Countries");
        }
    }
}
