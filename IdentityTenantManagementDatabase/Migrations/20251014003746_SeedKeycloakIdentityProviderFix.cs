using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityTenantManagementDatabase.Migrations
{
    /// <inheritdoc />
    public partial class SeedKeycloakIdentityProviderFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "IdentityProviders",
                keyColumn: "Id",
                keyValue: new Guid("049284c1-ff29-4f28-869f-f64300b69719"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 1, 37, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "IdentityProviders",
                keyColumn: "Id",
                keyValue: new Guid("049284c1-ff29-4f28-869f-f64300b69719"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 14, 0, 36, 9, 373, DateTimeKind.Utc).AddTicks(6069));
        }
    }
}
