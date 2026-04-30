using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class optimizationchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceTokens_AspNetUsers_UserId",
                table: "DeviceTokens");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_categoryId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_LoyaltyProgramId",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_TenantId",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_Customers_applicationUserId",
                table: "Customers");

            migrationBuilder.AlterColumn<decimal>(
                name: "Points",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "BalanceAfter",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "LifetimePoints",
                table: "LoyaltyCards",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentPoints",
                table: "LoyaltyCards",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "DeviceTokens",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "AppLogoUrl",
                table: "CardTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppStripUrl",
                table: "CardTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RadiusMeters",
                table: "Branches",
                type: "int",
                nullable: false,
                defaultValue: 100,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "Longitude",
                table: "Branches",
                type: "decimal(9,6)",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Latitude",
                table: "Branches",
                type: "decimal(9,6)",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsMainBranch",
                table: "Branches",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MenuUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WazeLocationUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppUrl",
                table: "Branches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    OrderIndex = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MinDeposit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "image"),
                    ThumbnailPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrderIndex = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomFiles_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Category_IsActive",
                table: "Tenants",
                columns: new[] { "categoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IsActive_IsHidden",
                table: "Tenants",
                columns: new[] { "IsActive", "IsHidden" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_Program_Tenant",
                table: "LoyaltyCards",
                columns: new[] { "LoyaltyProgramId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_Tenant_Customer_IsActive",
                table: "LoyaltyCards",
                columns: new[] { "TenantId", "CustomerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AppUser_IsActive",
                table: "Customers",
                columns: new[] { "applicationUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Tenant_IsActive",
                table: "Customers",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId",
                table: "Branches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomFiles_RoomId",
                table: "RoomFiles",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomFiles_RoomId_MediaType",
                table: "RoomFiles",
                columns: new[] { "RoomId", "MediaType" });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BranchId",
                table: "Rooms",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BranchId_IsActive",
                table: "Rooms",
                columns: new[] { "BranchId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceTokens_AspNetUsers_UserId",
                table: "DeviceTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceTokens_AspNetUsers_UserId",
                table: "DeviceTokens");

            migrationBuilder.DropTable(
                name: "RoomFiles");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Category_IsActive",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_IsActive_IsHidden",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_Program_Tenant",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyCards_Tenant_Customer_IsActive",
                table: "LoyaltyCards");

            migrationBuilder.DropIndex(
                name: "IX_Customers_AppUser_IsActive",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Tenant_IsActive",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Branches_TenantId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "AppLogoUrl",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "AppStripUrl",
                table: "CardTemplates");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "LocationUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "MenuUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TelegramUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "WazeLocationUrl",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "WhatsAppUrl",
                table: "Branches");

            migrationBuilder.AlterColumn<int>(
                name: "Points",
                table: "Transactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "BalanceAfter",
                table: "Transactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "LifetimePoints",
                table: "LoyaltyCards",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "CurrentPoints",
                table: "LoyaltyCards",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "DeviceTokens",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RadiusMeters",
                table: "Branches",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 100);

            migrationBuilder.AlterColumn<double>(
                name: "Longitude",
                table: "Branches",
                type: "float",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Latitude",
                table: "Branches",
                type: "float",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsMainBranch",
                table: "Branches",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_categoryId",
                table: "Tenants",
                column: "categoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_LoyaltyProgramId",
                table: "LoyaltyCards",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyCards_TenantId",
                table: "LoyaltyCards",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_applicationUserId",
                table: "Customers",
                column: "applicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceTokens_AspNetUsers_UserId",
                table: "DeviceTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
