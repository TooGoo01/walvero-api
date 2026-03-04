using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationTypeAndTenantIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DefaultBaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantIntegrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    IntegrationTypeId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalVenueId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LoyaltyProgramId = table.Column<int>(type: "int", nullable: true),
                    ApiBaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApiTokenEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WalletTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    WalletTokenEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantIntegrations_IntegrationTypes_IntegrationTypeId",
                        column: x => x.IntegrationTypeId,
                        principalTable: "IntegrationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantIntegrations_LoyaltyPrograms_LoyaltyProgramId",
                        column: x => x.LoyaltyProgramId,
                        principalTable: "LoyaltyPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TenantIntegrations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationTypes_Source",
                table: "IntegrationTypes",
                column: "Source",
                unique: true);

            migrationBuilder.InsertData(
                table: "IntegrationTypes",
                columns: new[] { "Name", "Source", "DefaultBaseUrl", "RegUserId", "IsActive", "RegDate" },
                values: new object[] { "Monyo POS", "monyo_pos", "https://monyo.az/v4/api", 1, true, new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrations_IntegrationTypeId",
                table: "TenantIntegrations",
                column: "IntegrationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrations_LoyaltyProgramId",
                table: "TenantIntegrations",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrations_TenantId_IntegrationTypeId",
                table: "TenantIntegrations",
                columns: new[] { "TenantId", "IntegrationTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrations_WalletTokenHash",
                table: "TenantIntegrations",
                column: "WalletTokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantIntegrations");

            migrationBuilder.DropTable(
                name: "IntegrationTypes");
        }
    }
}
