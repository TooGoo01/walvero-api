using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuPopups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuPopups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AzTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EnTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RuTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AzDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EnDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RuDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CtaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CtaUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AzCtaText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnCtaText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RuCtaText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuPopups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuPopups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuPopups_TenantId_IsEnabled_SortOrder",
                table: "MenuPopups",
                columns: new[] { "TenantId", "IsEnabled", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuPopups");
        }
    }
}
