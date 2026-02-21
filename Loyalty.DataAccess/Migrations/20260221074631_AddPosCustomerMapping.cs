using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPosCustomerMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosCustomerMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantIntegrationId = table.Column<int>(type: "int", nullable: false),
                    ExternalCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosCustomerMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosCustomerMappings_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PosCustomerMappings_TenantIntegrations_TenantIntegrationId",
                        column: x => x.TenantIntegrationId,
                        principalTable: "TenantIntegrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosCustomerMappings_CustomerId",
                table: "PosCustomerMappings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PosCustomerMappings_TenantIntegrationId_ExternalCustomerId",
                table: "PosCustomerMappings",
                columns: new[] { "TenantIntegrationId", "ExternalCustomerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosCustomerMappings");
        }
    }
}
