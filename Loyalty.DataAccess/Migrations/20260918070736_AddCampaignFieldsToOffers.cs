using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignFieldsToOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AzTerms",
                table: "Offers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountPercent",
                table: "Offers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnTerms",
                table: "Offers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuTerms",
                table: "Offers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidFrom",
                table: "Offers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidTo",
                table: "Offers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AzTerms",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "EnTerms",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "RuTerms",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "Offers");
        }
    }
}
