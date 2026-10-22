using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddIikoPhoneMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MatchExistingByPhone",
                table: "TenantIntegrations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SendPhoneToPos",
                table: "TenantIntegrations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LegacyBalance",
                table: "PosCustomerMappings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LegacyImportedAt",
                table: "PosCustomerMappings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyWalletId",
                table: "PosCustomerMappings",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatchExistingByPhone",
                table: "TenantIntegrations");

            migrationBuilder.DropColumn(
                name: "SendPhoneToPos",
                table: "TenantIntegrations");

            migrationBuilder.DropColumn(
                name: "LegacyBalance",
                table: "PosCustomerMappings");

            migrationBuilder.DropColumn(
                name: "LegacyImportedAt",
                table: "PosCustomerMappings");

            migrationBuilder.DropColumn(
                name: "LegacyWalletId",
                table: "PosCustomerMappings");
        }
    }
}
