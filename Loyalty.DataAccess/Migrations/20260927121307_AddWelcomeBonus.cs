using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddWelcomeBonus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FirstPurchaseBonusEnabled",
                table: "LoyaltyPrograms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstPurchaseBonusMaxPoints",
                table: "LoyaltyPrograms",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstPurchaseBonusType",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstPurchaseBonusValue",
                table: "LoyaltyPrograms",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SignupGiftEnabled",
                table: "LoyaltyPrograms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SignupGiftType",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SignupGiftValue",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPurchaseBonusGrantedAt",
                table: "LoyaltyCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignupGiftGrantedAt",
                table: "LoyaltyCards",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstPurchaseBonusEnabled",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "FirstPurchaseBonusMaxPoints",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "FirstPurchaseBonusType",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "FirstPurchaseBonusValue",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "SignupGiftEnabled",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "SignupGiftType",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "SignupGiftValue",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "FirstPurchaseBonusGrantedAt",
                table: "LoyaltyCards");

            migrationBuilder.DropColumn(
                name: "SignupGiftGrantedAt",
                table: "LoyaltyCards");
        }
    }
}
