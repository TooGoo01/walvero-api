using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAmbassadorSegment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmbassadorIikoCategoryId",
                table: "TenantIntegrations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyProgramId",
                table: "PosProductEarnRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Audience",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: false,
                defaultValue: 1); // ProgramAudience.Public — mövcud proqramlar adi qalır

            migrationBuilder.AddColumn<int>(
                name: "PosEarnSource",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PosExternalProgramId",
                table: "LoyaltyPrograms",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosWalletId",
                table: "LoyaltyPrograms",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepsPerOrder",
                table: "LoyaltyPrograms",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosProductEarnRules_LoyaltyProgramId",
                table: "PosProductEarnRules",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPrograms_Tenant_AmbassadorActive",
                table: "LoyaltyPrograms",
                columns: new[] { "TenantId", "Audience" },
                unique: true,
                filter: "[Audience] = 2 AND [IsActive] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_PosProductEarnRules_LoyaltyPrograms_LoyaltyProgramId",
                table: "PosProductEarnRules",
                column: "LoyaltyProgramId",
                principalTable: "LoyaltyPrograms",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosProductEarnRules_LoyaltyPrograms_LoyaltyProgramId",
                table: "PosProductEarnRules");

            migrationBuilder.DropIndex(
                name: "IX_PosProductEarnRules_LoyaltyProgramId",
                table: "PosProductEarnRules");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyPrograms_Tenant_AmbassadorActive",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "AmbassadorIikoCategoryId",
                table: "TenantIntegrations");

            migrationBuilder.DropColumn(
                name: "LoyaltyProgramId",
                table: "PosProductEarnRules");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "PosEarnSource",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "PosExternalProgramId",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "PosWalletId",
                table: "LoyaltyPrograms");

            migrationBuilder.DropColumn(
                name: "StepsPerOrder",
                table: "LoyaltyPrograms");
        }
    }
}
