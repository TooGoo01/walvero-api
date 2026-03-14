using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddLotteryFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLotteryEnabled",
                table: "LoyaltyPrograms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LotteryAmountThreshold",
                table: "LoyaltyPrograms",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LotteryPurchaseThreshold",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LotteryAmountAccum",
                table: "LoyaltyCards",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LotteryEarnCount",
                table: "LoyaltyCards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LotteryDraws",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    LoyaltyProgramId = table.Column<int>(type: "int", nullable: false),
                    DrawName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrizeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrizeDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WinnerCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotteryDraws", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotteryDraws_LoyaltyPrograms_LoyaltyProgramId",
                        column: x => x.LoyaltyProgramId,
                        principalTable: "LoyaltyPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotteryTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoyaltyCardId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    TicketCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LotteryDrawId = table.Column<int>(type: "int", nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotteryTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotteryTickets_LotteryDraws_LotteryDrawId",
                        column: x => x.LotteryDrawId,
                        principalTable: "LotteryDraws",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LotteryTickets_LoyaltyCards_LoyaltyCardId",
                        column: x => x.LoyaltyCardId,
                        principalTable: "LoyaltyCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotteryDraws_LoyaltyProgramId",
                table: "LotteryDraws",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_LotteryTickets_LotteryDrawId",
                table: "LotteryTickets",
                column: "LotteryDrawId");

            migrationBuilder.CreateIndex(
                name: "IX_LotteryTickets_LoyaltyCardId",
                table: "LotteryTickets",
                column: "LoyaltyCardId");

            migrationBuilder.CreateIndex(
                name: "IX_LotteryTickets_TenantId_Status",
                table: "LotteryTickets",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LotteryTickets_TicketCode",
                table: "LotteryTickets",
                column: "TicketCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotteryTickets");

            migrationBuilder.DropTable(
                name: "LotteryDraws");

            migrationBuilder.DropColumn(
                name: "IsLotteryEnabled",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "LotteryAmountThreshold",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "LotteryPurchaseThreshold",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "LotteryAmountAccum",
                table: "LoyaltyCards");

            migrationBuilder.DropColumn(
                name: "LotteryEarnCount",
                table: "LoyaltyCards");
        }
    }
}
