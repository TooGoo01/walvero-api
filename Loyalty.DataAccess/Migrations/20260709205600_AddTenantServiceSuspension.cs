using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantServiceSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsServiceSuspended",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionMessage",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsServiceSuspended",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SuspensionMessage",
                table: "Tenants");
        }
    }
}
