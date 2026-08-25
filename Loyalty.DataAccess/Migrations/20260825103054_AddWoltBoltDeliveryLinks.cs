using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddWoltBoltDeliveryLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoltFoodUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WoltUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoltFoodUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WoltUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoltFoodUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "WoltUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BoltFoodUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "WoltUrl",
                table: "Branches");
        }
    }
}
