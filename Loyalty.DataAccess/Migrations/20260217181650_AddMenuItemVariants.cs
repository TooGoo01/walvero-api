using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuItemVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    VariantType = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AzName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EnName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RuName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Price = table.Column<double>(type: "float", nullable: false),
                    DiscountPrice = table.Column<double>(type: "float", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemVariants_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemVariants_MenuItemId_VariantType_Code",
                table: "MenuItemVariants",
                columns: new[] { "MenuItemId", "VariantType", "Code" },
                unique: true);

            // Backfill: create one default "Regular" variant for each MenuItem that has no variants.
            // Idempotent: NOT EXISTS ensures no duplicates if migration is rerun.
            migrationBuilder.Sql(@"
                INSERT INTO MenuItemVariants (MenuItemId, VariantType, Code, AzName, EnName, RuName, SortOrder, IsDefault, Price, DiscountPrice, RegUserId, IsActive, RegDate)
                SELECT m.Id, 1, 'Regular', N'Standart', 'Regular', N'Стандарт', 1, 1, m.Price, m.DiscountPrice, 1, 1, GETUTCDATE()
                FROM MenuItems m
                WHERE NOT EXISTS (SELECT 1 FROM MenuItemVariants v WHERE v.MenuItemId = m.Id)
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemVariants");
        }
    }
}
