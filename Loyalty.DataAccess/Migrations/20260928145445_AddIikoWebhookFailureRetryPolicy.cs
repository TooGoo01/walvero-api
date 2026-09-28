using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddIikoWebhookFailureRetryPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TerminalGroupId",
                table: "IikoWebhookFailures",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "IikoWebhookFailures",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "IikoWebhookFailures",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailureKind",
                table: "IikoWebhookFailures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "IikoWebhookFailures",
                type: "datetime2",
                nullable: true);

            // Mövcud Pending sətirlər yeni retry sorğusuna (NextAttemptAt <= now) dərhal uyğun olsun
            migrationBuilder.Sql("UPDATE [IikoWebhookFailures] SET [NextAttemptAt] = GETUTCDATE() WHERE [Status] = 0 AND [NextAttemptAt] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_IikoWebhookFailures_OrderId_EventType",
                table: "IikoWebhookFailures",
                columns: new[] { "OrderId", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_IikoWebhookFailures_Status_NextAttemptAt",
                table: "IikoWebhookFailures",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IikoWebhookFailures_OrderId_EventType",
                table: "IikoWebhookFailures");

            migrationBuilder.DropIndex(
                name: "IX_IikoWebhookFailures_Status_NextAttemptAt",
                table: "IikoWebhookFailures");

            migrationBuilder.DropColumn(
                name: "FailureKind",
                table: "IikoWebhookFailures");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "IikoWebhookFailures");

            migrationBuilder.AlterColumn<string>(
                name: "TerminalGroupId",
                table: "IikoWebhookFailures",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "IikoWebhookFailures",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "IikoWebhookFailures",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }
    }
}
