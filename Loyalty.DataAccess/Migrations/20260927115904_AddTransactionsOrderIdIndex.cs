using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionsOrderIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Qoruyucu: nvarchar(max) → nvarchar(450) daralması 450-dən uzun dəyərdə xəta verər.
            // Belə dəyər gözlənilmir (idempotency açarları ~70 simvol), amma migration-ı partlatmasın.
            migrationBuilder.Sql(
                "UPDATE [Transactions] SET [OrderId] = LEFT([OrderId], 450) WHERE [OrderId] IS NOT NULL AND LEN([OrderId]) > 450;");

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "Transactions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OrderId",
                table: "Transactions",
                column: "OrderId",
                filter: "[OrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_OrderId",
                table: "Transactions");

            migrationBuilder.AlterColumn<string>(
                name: "OrderId",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);
        }
    }
}
