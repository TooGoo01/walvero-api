using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthdayGiftTypeAndNotificationRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppTargets",
                table: "TenantNotificationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WalletTargets",
                table: "TenantNotificationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DaysBefore",
                table: "TenantBirthdaySettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GiftLabel",
                table: "TenantBirthdaySettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GiftType",
                table: "TenantBirthdaySettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "GiftType",
                table: "BirthdayNotificationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantNotificationRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantNotificationLogId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    LoyaltyCardId = table.Column<int>(type: "int", nullable: true),
                    AppTarget = table.Column<bool>(type: "bit", nullable: false),
                    AppSent = table.Column<bool>(type: "bit", nullable: false),
                    WalletTarget = table.Column<bool>(type: "bit", nullable: false),
                    WalletSent = table.Column<bool>(type: "bit", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantNotificationRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantNotificationRecipients_TenantNotificationLogs_TenantNotificationLogId",
                        column: x => x.TenantNotificationLogId,
                        principalTable: "TenantNotificationLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotificationRecipients_TenantId_CustomerId",
                table: "TenantNotificationRecipients",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotificationRecipients_TenantNotificationLogId",
                table: "TenantNotificationRecipients",
                column: "TenantNotificationLogId");

            // Data fix (köhnə davranışı qoruyur): yalnız möhür proqramı (ProgressBased, RewardAsPoints=0) olan tenant-ların
            // mövcud ad günü ayarı → GiftType = 2 (Stamps); qarışıq/xal tenant-lar default 1 (Points) qalır.
            // Köhnə hədiyyə logları: hədiyyə yazılan kartın şablonuna görə növ (statistika üçün).
            migrationBuilder.Sql(@"
UPDATE s SET s.GiftType = 2
FROM TenantBirthdaySettings s
WHERE EXISTS (SELECT 1 FROM LoyaltyPrograms p JOIN CardTemplates t ON t.LoyaltyProgramId = p.Id
              WHERE p.TenantId = s.TenantId AND t.TemplateType = 1 AND t.RewardAsPoints = 0)
  AND NOT EXISTS (SELECT 1 FROM LoyaltyPrograms p JOIN CardTemplates t ON t.LoyaltyProgramId = p.Id
                  WHERE p.TenantId = s.TenantId AND NOT (t.TemplateType = 1 AND t.RewardAsPoints = 0));

UPDATE l SET l.GiftType = CASE WHEN t.TemplateType = 1 AND t.RewardAsPoints = 0 THEN 2 ELSE 1 END
FROM BirthdayNotificationLogs l
JOIN LoyaltyCards c ON c.Id = l.LoyaltyCardId
JOIN CardTemplates t ON t.LoyaltyProgramId = c.LoyaltyProgramId
WHERE l.GiftAmount IS NOT NULL AND l.GiftType IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantNotificationRecipients");

            migrationBuilder.DropColumn(
                name: "AppTargets",
                table: "TenantNotificationLogs");

            migrationBuilder.DropColumn(
                name: "WalletTargets",
                table: "TenantNotificationLogs");

            migrationBuilder.DropColumn(
                name: "DaysBefore",
                table: "TenantBirthdaySettings");

            migrationBuilder.DropColumn(
                name: "GiftLabel",
                table: "TenantBirthdaySettings");

            migrationBuilder.DropColumn(
                name: "GiftType",
                table: "TenantBirthdaySettings");

            migrationBuilder.DropColumn(
                name: "GiftType",
                table: "BirthdayNotificationLogs");
        }
    }
}
