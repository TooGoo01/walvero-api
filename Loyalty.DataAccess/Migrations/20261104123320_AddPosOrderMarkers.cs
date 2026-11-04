using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPosOrderMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosOrderMarkers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoyaltyCardId = table.Column<int>(type: "int", nullable: false),
                    OrderKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ExternalOrderId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TotalSum = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EarnedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WalletRedeem = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountRedeemUnits = table.Column<int>(type: "int", nullable: false),
                    WelcomeBonus = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosOrderMarkers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosOrderMarkers_LoyaltyCardId",
                table: "PosOrderMarkers",
                column: "LoyaltyCardId");

            migrationBuilder.CreateIndex(
                name: "IX_PosOrderMarkers_OrderKey",
                table: "PosOrderMarkers",
                column: "OrderKey",
                unique: true);

            // Köhnə idempotency marker tranzaksiyaları ("... OrderClosed marker (...)", Adjusted, balansı dəyişmir)
            // yeni cədvələ daşınır və Transactions-dan silinir — tarixçədə dublikat "ödəniş" kimi görünür,
            // statistikada sayılırdı. Eyni OrderId-li təkrar markerlərdən yalnız ilkini götürürük (unikal indeks).
            migrationBuilder.Sql(@"
;WITH legacy AS (
    SELECT t.Id, t.LoyaltyCardId, t.OrderId, t.ReferenceNumber, t.Amount, t.BalanceAfter, t.BranchId,
           t.Description, t.RegDate, t.RegUserId,
           ROW_NUMBER() OVER (PARTITION BY t.OrderId ORDER BY t.Id) AS rn
    FROM Transactions t
    WHERE t.OrderId IS NOT NULL
      AND t.Description LIKE '% OrderClosed marker%'
)
INSERT INTO PosOrderMarkers
    (LoyaltyCardId, OrderKey, Source, ExternalOrderId, EventType, OrderNumber, TotalSum,
     EarnedValue, WalletRedeem, DiscountRedeemUnits, WelcomeBonus, BalanceAfter, BranchId, Note,
     RegUserId, IsActive, RegDate)
SELECT l.LoyaltyCardId,
       l.OrderId,
       LEFT(l.OrderId, NULLIF(CHARINDEX(':', l.OrderId), 0) - 1),
       NULL,
       'OrderClosed',
       LEFT(l.ReferenceNumber, 32),
       ISNULL(l.Amount, 0),
       0, 0, 0, 0,
       l.BalanceAfter,
       l.BranchId,
       LEFT(l.Description, 500),
       ISNULL(l.RegUserId, 1), 1, l.RegDate
FROM legacy l
WHERE l.rn = 1
  AND NOT EXISTS (SELECT 1 FROM PosOrderMarkers m WHERE m.OrderKey = l.OrderId);

DELETE FROM Transactions
WHERE OrderId IS NOT NULL
  AND Description LIKE '% OrderClosed marker%';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosOrderMarkers");
        }
    }
}
