using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class show_hometenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if Prefix column exists before adding (idempotent migration)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 
                    FROM sys.columns 
                    WHERE object_id = OBJECT_ID('dbo.TenantCustomerSequences') 
                    AND name = 'Prefix'
                )
                BEGIN
                    ALTER TABLE dbo.TenantCustomerSequences
                    ADD Prefix NVARCHAR(5) NOT NULL DEFAULT '789';
                END
            ");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TenantCustomerSequences",
                type: "varbinary(max)",
                nullable: true);

            // Check if UpdatedAt column exists before adding (idempotent migration)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 
                    FROM sys.columns 
                    WHERE object_id = OBJECT_ID('dbo.TenantCustomerSequences') 
                    AND name = 'UpdatedAt'
                )
                BEGIN
                    ALTER TABLE dbo.TenantCustomerSequences
                    ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
                END
            ");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CountrySequences",
                type: "varbinary(max)",
                nullable: true);

            // Check if UpdatedAt column exists before adding (idempotent migration)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 
                    FROM sys.columns 
                    WHERE object_id = OBJECT_ID('dbo.CountrySequences') 
                    AND name = 'UpdatedAt'
                )
                BEGIN
                    ALTER TABLE dbo.CountrySequences
                    ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Prefix",
                table: "TenantCustomerSequences");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TenantCustomerSequences");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TenantCustomerSequences");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CountrySequences");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CountrySequences");
        }
    }
}
