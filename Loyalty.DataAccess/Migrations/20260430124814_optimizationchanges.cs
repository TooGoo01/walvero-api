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
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_DeviceTokens_AspNetUsers_UserId' AND parent_object_id=OBJECT_ID(N'[DeviceTokens]')) ALTER TABLE [DeviceTokens] DROP CONSTRAINT [FK_DeviceTokens_AspNetUsers_UserId];");

            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tenants_categoryId' AND object_id = OBJECT_ID(N'[Tenants]')) DROP INDEX [IX_Tenants_categoryId] ON [Tenants];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LoyaltyCards_LoyaltyProgramId' AND object_id = OBJECT_ID(N'[LoyaltyCards]')) DROP INDEX [IX_LoyaltyCards_LoyaltyProgramId] ON [LoyaltyCards];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LoyaltyCards_TenantId' AND object_id = OBJECT_ID(N'[LoyaltyCards]')) DROP INDEX [IX_LoyaltyCards_TenantId] ON [LoyaltyCards];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Customers_applicationUserId' AND object_id = OBJECT_ID(N'[Customers]')) DROP INDEX [IX_Customers_applicationUserId] ON [Customers];");

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

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[CardTemplates]', N'AppLogoUrl') IS NULL ALTER TABLE [CardTemplates] ADD [AppLogoUrl] nvarchar(max) NULL;");

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[CardTemplates]', N'AppStripUrl') IS NULL ALTER TABLE [CardTemplates] ADD [AppStripUrl] nvarchar(max) NULL;");

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

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[Branches]', N'FacebookUrl') IS NULL ALTER TABLE [Branches] ADD [FacebookUrl] nvarchar(500) NULL;");

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[Branches]', N'InstagramUrl') IS NULL ALTER TABLE [Branches] ADD [InstagramUrl] nvarchar(500) NULL;");

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[Branches]', N'LinkedInUrl') IS NULL ALTER TABLE [Branches] ADD [LinkedInUrl] nvarchar(500) NULL;");

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[Branches]', N'LocationUrl') IS NULL ALTER TABLE [Branches] ADD [LocationUrl] nvarchar(500) NULL;");

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[Branches]', N'LogoUrl') IS NULL ALTER TABLE [Branches] ADD [LogoUrl] nvarchar(500) NULL;");

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[Branches]', N'MenuUrl') IS NULL ALTER TABLE [Branches] ADD [MenuUrl] nvarchar(500) NULL;");

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[Branches]', N'TelegramUrl') IS NULL ALTER TABLE [Branches] ADD [TelegramUrl] nvarchar(500) NULL;");

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[Branches]', N'WazeLocationUrl') IS NULL ALTER TABLE [Branches] ADD [WazeLocationUrl] nvarchar(500) NULL;");

            migrationBuilder.Sql(@"IF COL_LENGTH(N'[Branches]', N'WhatsAppUrl') IS NULL ALTER TABLE [Branches] ADD [WhatsAppUrl] nvarchar(500) NULL;");

            migrationBuilder.Sql(@"IF OBJECT_ID(N'[Rooms]', N'U') IS NULL BEGIN CREATE TABLE [Rooms] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [BranchId] int NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Type] nvarchar(100) NULL,
    [Description] nvarchar(2000) NULL,
    [Capacity] int NULL,
    [OrderIndex] int NOT NULL DEFAULT (0),
    [MinDeposit] decimal(18,2) NULL,
    [RegUserId] int NOT NULL,
    [IsActive] bit NOT NULL,
    [RegDate] datetime2 NOT NULL,
    [EditUserId] int NULL,
    [EditDate] datetime2 NULL,
    CONSTRAINT [PK_Rooms] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Rooms_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE CASCADE
); END;");

            migrationBuilder.Sql(@"IF OBJECT_ID(N'[RoomFiles]', N'U') IS NULL BEGIN CREATE TABLE [RoomFiles] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [RoomId] int NOT NULL,
    [MediaType] nvarchar(20) NOT NULL DEFAULT (N'image'),
    [ThumbnailPath] nvarchar(1000) NULL,
    [OrderIndex] int NOT NULL DEFAULT (0),
    [RegUserId] int NOT NULL,
    [IsActive] bit NOT NULL,
    [RegDate] datetime2 NOT NULL,
    [EditUserId] int NULL,
    [EditDate] datetime2 NULL,
    [FileName] nvarchar(500) NOT NULL,
    [Path] nvarchar(1000) NOT NULL,
    CONSTRAINT [PK_RoomFiles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoomFiles_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([Id]) ON DELETE CASCADE
); END;");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Tenants_Category_IsActive' AND object_id=OBJECT_ID(N'[Tenants]')) CREATE INDEX [IX_Tenants_Category_IsActive] ON [Tenants] ([categoryId], [IsActive]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Tenants_IsActive_IsHidden' AND object_id=OBJECT_ID(N'[Tenants]')) CREATE INDEX [IX_Tenants_IsActive_IsHidden] ON [Tenants] ([IsActive], [IsHidden]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LoyaltyCards_Program_Tenant' AND object_id=OBJECT_ID(N'[LoyaltyCards]')) CREATE INDEX [IX_LoyaltyCards_Program_Tenant] ON [LoyaltyCards] ([LoyaltyProgramId], [TenantId]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_LoyaltyCards_Tenant_Customer_IsActive' AND object_id=OBJECT_ID(N'[LoyaltyCards]')) CREATE INDEX [IX_LoyaltyCards_Tenant_Customer_IsActive] ON [LoyaltyCards] ([TenantId], [CustomerId], [IsActive]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Customers_AppUser_IsActive' AND object_id=OBJECT_ID(N'[Customers]')) CREATE INDEX [IX_Customers_AppUser_IsActive] ON [Customers] ([applicationUserId], [IsActive]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Customers_Tenant_IsActive' AND object_id=OBJECT_ID(N'[Customers]')) CREATE INDEX [IX_Customers_Tenant_IsActive] ON [Customers] ([TenantId], [IsActive]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Branches_TenantId' AND object_id=OBJECT_ID(N'[Branches]')) CREATE INDEX [IX_Branches_TenantId] ON [Branches] ([TenantId]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RoomFiles_RoomId' AND object_id=OBJECT_ID(N'[RoomFiles]')) CREATE INDEX [IX_RoomFiles_RoomId] ON [RoomFiles] ([RoomId]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RoomFiles_RoomId_MediaType' AND object_id=OBJECT_ID(N'[RoomFiles]')) CREATE INDEX [IX_RoomFiles_RoomId_MediaType] ON [RoomFiles] ([RoomId], [MediaType]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Rooms_BranchId' AND object_id=OBJECT_ID(N'[Rooms]')) CREATE INDEX [IX_Rooms_BranchId] ON [Rooms] ([BranchId]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Rooms_BranchId_IsActive' AND object_id=OBJECT_ID(N'[Rooms]')) CREATE INDEX [IX_Rooms_BranchId_IsActive] ON [Rooms] ([BranchId], [IsActive]);");

            migrationBuilder.Sql(@"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_DeviceTokens_AspNetUsers_UserId' AND parent_object_id=OBJECT_ID(N'[DeviceTokens]')) ALTER TABLE [DeviceTokens] ADD CONSTRAINT [FK_DeviceTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL;");
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
