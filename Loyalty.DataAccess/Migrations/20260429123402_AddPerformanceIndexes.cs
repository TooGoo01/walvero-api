using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_categoryId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_LoyaltyProgramId",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_TenantId",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_Customers_applicationUserId",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Category_IsActive",
                table: "Tenants",
                columns: new[] { "categoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsActive_IsHidden",
                table: "Tenants",
                columns: new[] { "IsActive", "IsHidden" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_Program_Tenant",
                table: "LoyaltyCards",
                columns: new[] { "LoyaltyProgramId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_Tenant_Customer_IsActive",
                table: "LoyaltyCards",
                columns: new[] { "TenantId", "CustomerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AppUser_IsActive",
                table: "Customers",
                columns: new[] { "applicationUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Tenant_IsActive",
                table: "Customers",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Category_IsActive",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_IsActive_IsHidden",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_Program_Tenant",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_Tenant_Customer_IsActive",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_Customers_AppUser_IsActive",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Tenant_IsActive",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_categoryId",
                table: "Tenants",
                column: "categoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_LoyaltyProgramId",
                table: "LoyaltyCards",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_TenantId",
                table: "LoyaltyCards",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_applicationUserId",
                table: "Customers",
                column: "applicationUserId");
        }
    }
}
