using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxNumberAndMultiLangLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedTaxNumber",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedTaxNumber",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrLabelTaxNumber",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "VÖEN,VOEN,Vergi nömrəsi,Tax number,TIN");

            // Mövcud tenant-larda köhnə tək-label dəyərlərini comma-list multi-lang versiyasına yenilə.
            // Yalnız əvvəlki default tək-label-ə bərabər olanlar dəyişir (admin manuel
            // dəyişmiş tenant-lar toxunulmaz qalır).
            migrationBuilder.Sql(
                "UPDATE Tenants SET OcrLabelAmount = 'Yekun Məbləğ,Cəmi,Total,Sum' WHERE OcrLabelAmount = 'Yekun Məbləğ';");
            migrationBuilder.Sql(
                "UPDATE Tenants SET OcrLabelObjectCode = 'Obyektin kodu,Obyekt Kodu,Object Code' WHERE OcrLabelObjectCode = 'Obyekt Kodu';");
            migrationBuilder.Sql(
                "UPDATE Tenants SET OcrLabelReceiptId = 'Fiskal İD,Fiskal ID,Fiscal ID,Çek nömrəsi' WHERE OcrLabelReceiptId = 'Çek nömrəsi';");

            migrationBuilder.AddColumn<string>(
                name: "ExtractedTaxNumber",
                table: "RedeemRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedTaxNumber",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrLabelTaxNumber",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedTaxNumber",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExpectedTaxNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OcrLabelTaxNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ExtractedTaxNumber",
                table: "RedeemRequests");

            migrationBuilder.DropColumn(
                name: "ExpectedTaxNumber",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "OcrLabelTaxNumber",
                table: "Branches");
        }
    }
}
