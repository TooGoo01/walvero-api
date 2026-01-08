using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class walvero_tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

          

          

            migrationBuilder.CreateTable(
                name: "WalveroWalletPasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationUserId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    WalveroIdMasked = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApplePassUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GoogleSaveUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AppleSerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GoogleObjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HasIosPass = table.Column<bool>(type: "bit", nullable: false),
                    HasAndroidPass = table.Column<bool>(type: "bit", nullable: false),
                    LastGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalveroWalletPasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalveroWalletPasses_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalveroWalletPasses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

        

            migrationBuilder.CreateIndex(
                name: "IX_WalveroWalletPasses_ApplicationUserId",
                table: "WalveroWalletPasses",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalveroWalletPasses_ApplicationUserId_TenantId",
                table: "WalveroWalletPasses",
                columns: new[] { "ApplicationUserId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalveroWalletPasses_TenantId",
                table: "WalveroWalletPasses",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
           

            migrationBuilder.DropTable(
                name: "WalveroWalletPasses");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Tenants");

        }
    }
}
