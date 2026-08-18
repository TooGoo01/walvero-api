using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalTerminalGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalTerminalGroupId",
                table: "TenantIntegrations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrations_ExternalTerminalGroupId",
                table: "TenantIntegrations",
                column: "ExternalTerminalGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantIntegrations_ExternalTerminalGroupId",
                table: "TenantIntegrations");

            migrationBuilder.DropColumn(
                name: "ExternalTerminalGroupId",
                table: "TenantIntegrations");
        }
    }
}
