using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddQrVerificationDynamicLabelsAndReceiptId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedObjectCode",
                table: "Transactions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalReceiptId",
                table: "Transactions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrDocId",
                table: "Transactions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QrVerified",
                table: "Transactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptDate",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyUserOnMissingQr",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OcrLabelAmount",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Yekun Məbləğ");

            migrationBuilder.AddColumn<string>(
                name: "OcrLabelObjectCode",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Obyekt Kodu");

            migrationBuilder.AddColumn<string>(
                name: "OcrLabelReceiptId",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Çek nömrəsi");

            migrationBuilder.AddColumn<string>(
                name: "ReceiptTimeZoneId",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Azerbaijan Standard Time");

            migrationBuilder.AddColumn<bool>(
                name: "RequireQrVerification",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedObjectCode",
                table: "RedeemRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalReceiptId",
                table: "RedeemRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrDocId",
                table: "RedeemRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QrVerified",
                table: "RedeemRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptDate",
                table: "RedeemRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyUserOnMissingQr",
                table: "Branches",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjectCode",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrLabelAmount",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrLabelObjectCode",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrLabelReceiptId",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptTimeZoneId",
                table: "Branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireQrVerification",
                table: "Branches",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PhysicalReceiptId",
                table: "Transactions",
                column: "PhysicalReceiptId",
                filter: "[PhysicalReceiptId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_PhysicalReceiptId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExtractedObjectCode",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PhysicalReceiptId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "QrDocId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "QrVerified",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReceiptDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "NotifyUserOnMissingQr",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OcrLabelAmount",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OcrLabelObjectCode",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OcrLabelReceiptId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ReceiptTimeZoneId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RequireQrVerification",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ExtractedObjectCode",
                table: "RedeemRequests");

            migrationBuilder.DropColumn(
                name: "PhysicalReceiptId",
                table: "RedeemRequests");

            migrationBuilder.DropColumn(
                name: "QrDocId",
                table: "RedeemRequests");

            migrationBuilder.DropColumn(
                name: "QrVerified",
                table: "RedeemRequests");

            migrationBuilder.DropColumn(
                name: "ReceiptDate",
                table: "RedeemRequests");

            migrationBuilder.DropColumn(
                name: "NotifyUserOnMissingQr",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ObjectCode",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "OcrLabelAmount",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "OcrLabelObjectCode",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "OcrLabelReceiptId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "ReceiptTimeZoneId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "RequireQrVerification",
                table: "Branches");
        }
    }
}
