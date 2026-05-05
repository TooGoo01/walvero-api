using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class add_tenant_links : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    AzTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RuTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IconKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    OpenInNewTab = table.Column<bool>(type: "bit", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantLinks_TenantId_SortOrder",
                table: "TenantLinks",
                columns: new[] { "TenantId", "SortOrder" });

            // Mövcud tenant-lardan TenantLink-ləri avtomatik doldur (idempotent).
            // Yalnız müvafiq URL doludursa link yaranır. Sıralama: Menu(0), Instagram(1),
            // Facebook(2), WhatsApp(3), Telegram(4), LinkedIn(5), Google Maps(6), Waze(7).
            migrationBuilder.Sql(@"
;WITH src(IconKey, AzTitle, EnTitle, RuTitle, SortOrder) AS (
    SELECT 'menu',      N'Menyu',   N'Menu',      N'Меню',      0 UNION ALL
    SELECT 'instagram', N'Instagram', N'Instagram', N'Instagram', 1 UNION ALL
    SELECT 'facebook',  N'Facebook',  N'Facebook',  N'Facebook',  2 UNION ALL
    SELECT 'whatsapp',  N'WhatsApp',  N'WhatsApp',  N'WhatsApp',  3 UNION ALL
    SELECT 'telegram',  N'Telegram',  N'Telegram',  N'Telegram',  4 UNION ALL
    SELECT 'linkedin',  N'LinkedIn',  N'LinkedIn',  N'LinkedIn',  5 UNION ALL
    SELECT 'location',  N'Xəritədə bax', N'Open in Maps', N'Открыть на карте', 6 UNION ALL
    SELECT 'waze',      N'Waze-də bax',  N'Open in Waze', N'Открыть в Waze',   7
)
INSERT INTO TenantLinks (TenantId, AzTitle, EnTitle, RuTitle, Url, IconKey, SortOrder, OpenInNewTab, IsActive, RegDate, RegUserId)
SELECT
    t.Id,
    s.AzTitle,
    s.EnTitle,
    s.RuTitle,
    CASE s.IconKey
        WHEN 'menu'      THEN t.MenuUrl
        WHEN 'instagram' THEN t.InstagramUrl
        WHEN 'facebook'  THEN t.FacebookUrl
        WHEN 'whatsapp'  THEN t.WhatsAppUrl
        WHEN 'telegram'  THEN t.TeleqramUrl
        WHEN 'linkedin'  THEN t.LinkedInUrl
        WHEN 'location'  THEN t.LocationUrl
        WHEN 'waze'      THEN t.WazeLocationUrl
    END AS Url,
    s.IconKey,
    s.SortOrder,
    1 AS OpenInNewTab,
    1 AS IsActive,
    SYSUTCDATETIME() AS RegDate,
    0 AS RegUserId
FROM Tenants t
CROSS JOIN src s
WHERE
    CASE s.IconKey
        WHEN 'menu'      THEN CASE WHEN t.IsHasMenu = 1 THEN t.MenuUrl ELSE NULL END
        WHEN 'instagram' THEN t.InstagramUrl
        WHEN 'facebook'  THEN t.FacebookUrl
        WHEN 'whatsapp'  THEN t.WhatsAppUrl
        WHEN 'telegram'  THEN t.TeleqramUrl
        WHEN 'linkedin'  THEN t.LinkedInUrl
        WHEN 'location'  THEN t.LocationUrl
        WHEN 'waze'      THEN t.WazeLocationUrl
    END IS NOT NULL
    AND LTRIM(RTRIM(CASE s.IconKey
        WHEN 'menu'      THEN CASE WHEN t.IsHasMenu = 1 THEN t.MenuUrl ELSE NULL END
        WHEN 'instagram' THEN t.InstagramUrl
        WHEN 'facebook'  THEN t.FacebookUrl
        WHEN 'whatsapp'  THEN t.WhatsAppUrl
        WHEN 'telegram'  THEN t.TeleqramUrl
        WHEN 'linkedin'  THEN t.LinkedInUrl
        WHEN 'location'  THEN t.LocationUrl
        WHEN 'waze'      THEN t.WazeLocationUrl
    END)) <> ''
    AND NOT EXISTS (
        SELECT 1 FROM TenantLinks tl
        WHERE tl.TenantId = t.Id AND tl.IconKey = s.IconKey
    );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantLinks");
        }
    }
}
