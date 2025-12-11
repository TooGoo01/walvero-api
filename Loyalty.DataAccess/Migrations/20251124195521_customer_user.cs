using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyWalletApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class customer_user : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "applicationUserId",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_applicationUserId",
                table: "Customers",
                column: "applicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_AspNetUsers_applicationUserId",
                table: "Customers",
                column: "applicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_AspNetUsers_applicationUserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_applicationUserId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "applicationUserId",
                table: "Customers");
        }
    }
}
