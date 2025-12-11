using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class credential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Icon_About_AboutId",
                table: "Icon");

            migrationBuilder.DropForeignKey(
                name: "FK_Icon_CardTemplates_CardTemplateId",
                table: "Icon");

            migrationBuilder.DropTable(
                name: "AboutFile");

            migrationBuilder.DropTable(
                name: "About");

            migrationBuilder.DropIndex(
                name: "IX_Icon_AboutId",
                table: "Icon");

            migrationBuilder.RenameColumn(
                name: "PrivateKeyPath",
                table: "WalletCredentials",
                newName: "GooglePrivateKeyUrl");

            migrationBuilder.RenameColumn(
                name: "CertificatePath",
                table: "WalletCredentials",
                newName: "AppleCertificateUrl");

            migrationBuilder.RenameColumn(
                name: "AboutId",
                table: "Icon",
                newName: "Order");

            migrationBuilder.AlterColumn<int>(
                name: "CardTemplateId",
                table: "Icon",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconUrl",
                table: "Icon",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Icon",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Icon_CardTemplates_CardTemplateId",
                table: "Icon",
                column: "CardTemplateId",
                principalTable: "CardTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Icon_CardTemplates_CardTemplateId",
                table: "Icon");

            migrationBuilder.DropColumn(
                name: "IconUrl",
                table: "Icon");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Icon");

            migrationBuilder.RenameColumn(
                name: "GooglePrivateKeyUrl",
                table: "WalletCredentials",
                newName: "PrivateKeyPath");

            migrationBuilder.RenameColumn(
                name: "AppleCertificateUrl",
                table: "WalletCredentials",
                newName: "CertificatePath");

            migrationBuilder.RenameColumn(
                name: "Order",
                table: "Icon",
                newName: "AboutId");

            migrationBuilder.AlterColumn<int>(
                name: "CardTemplateId",
                table: "Icon",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "About",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AzDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AzTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Facebook = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Instagram = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    RuDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Twitter = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_About", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AboutFile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AboutId = table.Column<int>(type: "int", nullable: false),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AboutFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AboutFile_About_AboutId",
                        column: x => x.AboutId,
                        principalTable: "About",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Icon_AboutId",
                table: "Icon",
                column: "AboutId");

            migrationBuilder.CreateIndex(
                name: "IX_AboutFile_AboutId",
                table: "AboutFile",
                column: "AboutId");

            migrationBuilder.AddForeignKey(
                name: "FK_Icon_About_AboutId",
                table: "Icon",
                column: "AboutId",
                principalTable: "About",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Icon_CardTemplates_CardTemplateId",
                table: "Icon",
                column: "CardTemplateId",
                principalTable: "CardTemplates",
                principalColumn: "Id");
        }
    }
}
