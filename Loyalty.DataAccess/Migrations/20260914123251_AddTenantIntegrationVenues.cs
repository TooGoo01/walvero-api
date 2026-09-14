using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIntegrationVenues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantIntegrationVenueId",
                table: "PosCustomerMappings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantIntegrationVenues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantIntegrationId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    ExternalVenueId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalTerminalGroupId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalProgramId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantIntegrationVenues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantIntegrationVenues_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenantIntegrationVenues_TenantIntegrations_TenantIntegrationId",
                        column: x => x.TenantIntegrationId,
                        principalTable: "TenantIntegrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosCustomerMappings_TenantIntegrationVenueId",
                table: "PosCustomerMappings",
                column: "TenantIntegrationVenueId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrationVenues_BranchId",
                table: "TenantIntegrationVenues",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrationVenues_ExternalTerminalGroupId",
                table: "TenantIntegrationVenues",
                column: "ExternalTerminalGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrationVenues_TenantIntegrationId_IsActive",
                table: "TenantIntegrationVenues",
                columns: new[] { "TenantIntegrationId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_PosCustomerMappings_TenantIntegrationVenues_TenantIntegrationVenueId",
                table: "PosCustomerMappings",
                column: "TenantIntegrationVenueId",
                principalTable: "TenantIntegrationVenues",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosCustomerMappings_TenantIntegrationVenues_TenantIntegrationVenueId",
                table: "PosCustomerMappings");

            migrationBuilder.DropTable(
                name: "TenantIntegrationVenues");

            migrationBuilder.DropIndex(
                name: "IX_PosCustomerMappings_TenantIntegrationVenueId",
                table: "PosCustomerMappings");

            migrationBuilder.DropColumn(
                name: "TenantIntegrationVenueId",
                table: "PosCustomerMappings");
        }
    }
}
