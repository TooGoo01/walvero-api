using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddEkassaJsonAuditTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EkassaJsonAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    TransactionId = table.Column<int>(type: "int", nullable: true),
                    QrDocId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParsedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ParsedObjectCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ParsedDocumentNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ParsedCreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkassaJsonAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EkassaJsonAudits_RegDate",
                table: "EkassaJsonAudits",
                column: "RegDate");

            migrationBuilder.CreateIndex(
                name: "IX_EkassaJsonAudits_TenantId",
                table: "EkassaJsonAudits",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EkassaJsonAudits");
        }
    }
}
