using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class contrycodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditDate",
                table: "TenantCustomerSequences");

            migrationBuilder.DropColumn(
                name: "EditUserId",
                table: "TenantCustomerSequences");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "TenantCustomerSequences");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TenantCustomerSequences");

            migrationBuilder.DropColumn(
                name: "RegDate",
                table: "TenantCustomerSequences");

            migrationBuilder.DropColumn(
                name: "RegUserId",
                table: "TenantCustomerSequences");

            migrationBuilder.DropColumn(
                name: "EditDate",
                table: "CountrySequences");

            migrationBuilder.DropColumn(
                name: "EditUserId",
                table: "CountrySequences");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "CountrySequences");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CountrySequences");

            migrationBuilder.DropColumn(
                name: "RegDate",
                table: "CountrySequences");

            migrationBuilder.DropColumn(
                name: "RegUserId",
                table: "CountrySequences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditDate",
                table: "TenantCustomerSequences",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EditUserId",
                table: "TenantCustomerSequences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "TenantCustomerSequences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TenantCustomerSequences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegDate",
                table: "TenantCustomerSequences",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RegUserId",
                table: "TenantCustomerSequences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditDate",
                table: "CountrySequences",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EditUserId",
                table: "CountrySequences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "CountrySequences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CountrySequences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegDate",
                table: "CountrySequences",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RegUserId",
                table: "CountrySequences",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
