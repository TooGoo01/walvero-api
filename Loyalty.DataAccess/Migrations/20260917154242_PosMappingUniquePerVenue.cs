using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class PosMappingUniquePerVenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosCustomerMappings_TenantIntegrationId_ExternalCustomerId",
                table: "PosCustomerMappings");

            migrationBuilder.CreateIndex(
                name: "IX_PosCustomerMappings_TenantIntegrationId_ExternalCustomerId_TenantIntegrationVenueId",
                table: "PosCustomerMappings",
                columns: new[] { "TenantIntegrationId", "ExternalCustomerId", "TenantIntegrationVenueId" },
                unique: true,
                filter: "[TenantIntegrationVenueId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosCustomerMappings_TenantIntegrationId_ExternalCustomerId_TenantIntegrationVenueId",
                table: "PosCustomerMappings");

            migrationBuilder.CreateIndex(
                name: "IX_PosCustomerMappings_TenantIntegrationId_ExternalCustomerId",
                table: "PosCustomerMappings",
                columns: new[] { "TenantIntegrationId", "ExternalCustomerId" },
                unique: true);
        }
    }
}
