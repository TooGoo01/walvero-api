using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinkPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BackgroundImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BackgroundColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    TextColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    PrimaryColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Layout = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FooterText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkPageClickEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LinkPageId = table.Column<int>(type: "int", nullable: false),
                    LinkPageItemId = table.Column<int>(type: "int", nullable: false),
                    ClickedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkPageClickEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkPageClickEvents_LinkPages_LinkPageId",
                        column: x => x.LinkPageId,
                        principalTable: "LinkPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinkPageItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LinkPageId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Style = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IconUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkPageItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkPageItems_LinkPages_LinkPageId",
                        column: x => x.LinkPageId,
                        principalTable: "LinkPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LinkPageClickEvents_LinkPageId_ClickedAt",
                table: "LinkPageClickEvents",
                columns: new[] { "LinkPageId", "ClickedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkPageClickEvents_LinkPageItemId",
                table: "LinkPageClickEvents",
                column: "LinkPageItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkPageItems_LinkPageId_SortOrder",
                table: "LinkPageItems",
                columns: new[] { "LinkPageId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkPages_Slug",
                table: "LinkPages",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkPageClickEvents");

            migrationBuilder.DropTable(
                name: "LinkPageItems");

            migrationBuilder.DropTable(
                name: "LinkPages");
        }
    }
}
