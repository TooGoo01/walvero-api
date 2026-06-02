using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddEkassaReceiptUrlTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EkassaReceiptUrlTemplate",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "https://monitoring.e-kassa.gov.az/pks-monitoring/2.0.0/documents/{docId}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EkassaReceiptUrlTemplate",
                table: "Tenants");
        }
    }
}
