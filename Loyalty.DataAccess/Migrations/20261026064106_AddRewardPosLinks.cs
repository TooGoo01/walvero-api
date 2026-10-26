using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardPosLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RewardId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosDiscountTypeId",
                table: "Rewards",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosSourceKey",
                table: "Rewards",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RewardPosProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RewardId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardPosProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardPosProducts_Rewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RewardPosProducts_RewardId_Code",
                table: "RewardPosProducts",
                columns: new[] { "RewardId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RewardPosProducts");

            migrationBuilder.DropColumn(
                name: "RewardId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PosDiscountTypeId",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "PosSourceKey",
                table: "Rewards");
        }
    }
}
