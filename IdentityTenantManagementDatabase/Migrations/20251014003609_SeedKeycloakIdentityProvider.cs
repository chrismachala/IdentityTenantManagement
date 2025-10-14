using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityTenantManagementDatabase.Migrations
{
    /// <inheritdoc />
    public partial class SeedKeycloakIdentityProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.InsertData(
                table: "IdentityProviders",
                columns: new[] { "Id", "BaseUrl", "CreatedAt", "Name", "ProviderType" },
                values: new object[] { new Guid("049284c1-ff29-4f28-869f-f64300b69719"), "http://localhost:8080/", new DateTime(2025, 10, 14, 0, 36, 9, 373, DateTimeKind.Utc).AddTicks(6069), "Keycloak", "oidc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DeleteData(
                table: "IdentityProviders",
                keyColumn: "Id",
                keyValue: new Guid("049284c1-ff29-4f28-869f-f64300b69719"));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
