using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <summary>
    /// Performance optimization migration for tenant search functionality.
    /// Adds indexes to improve query performance for country and city filtering.
    /// </summary>
    public partial class TenantSearchOptimizationIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Index for TenantLocation.countryId - improves country filtering
            migrationBuilder.CreateIndex(
                name: "IX_TenantLocations_countryId",
                table: "TenantLocations",
                column: "countryId");

            // Index for TenantLocation.cityId - improves city filtering
            migrationBuilder.CreateIndex(
                name: "IX_TenantLocations_cityId",
                table: "TenantLocations",
                column: "cityId");

            // Composite index for Tenant.IsActive and IsHidden - improves WHERE clause performance
            // Most queries filter by these two fields together
            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsActive_IsHidden",
                table: "Tenants",
                columns: new[] { "IsActive", "IsHidden" });

            // Index for Tenant.categoryId - improves category filtering
            migrationBuilder.CreateIndex(
                name: "IX_Tenants_categoryId",
                table: "Tenants",
                column: "categoryId");

            // Composite index for Tenant search with location
            // Improves queries that filter by both tenant status and location
            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsActive_IsHidden_tenantLocationId",
                table: "Tenants",
                columns: new[] { "IsActive", "IsHidden", "tenantLocationId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes in reverse order
            migrationBuilder.DropIndex(
                name: "IX_Tenants_IsActive_IsHidden_tenantLocationId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_categoryId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_IsActive_IsHidden",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_TenantLocations_cityId",
                table: "TenantLocations");

            migrationBuilder.DropIndex(
                name: "IX_TenantLocations_countryId",
                table: "TenantLocations");
        }
    }
}
