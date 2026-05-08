using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class brachchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TiktokUrl",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BasePercentCard",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BasePercentCash",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForegroundColor",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TiktokUrl",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HappyHoursWindows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoyaltyProgramId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: true),
                    SpecificDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    PercentCash = table.Column<int>(type: "int", nullable: false),
                    PercentCard = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HappyHoursWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HappyHoursWindows_LoyaltyPrograms_LoyaltyProgramId",
                        column: x => x.LoyaltyProgramId,
                        principalTable: "LoyaltyPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HappyHoursWindows_Program_Day_Date",
                table: "HappyHoursWindows",
                columns: new[] { "LoyaltyProgramId", "DayOfWeek", "SpecificDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HappyHoursWindows");

            migrationBuilder.DropColumn(
                name: "TiktokUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BasePercentCard",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "BasePercentCash",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ForegroundColor",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TiktokUrl",
                table: "Branches");
        }
    }
}
