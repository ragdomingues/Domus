using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeviceLifecycleAndHardwareUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_HardwareId",
                table: "devices");

            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                table: "devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_devices_HardwareId",
                table: "devices",
                column: "HardwareId",
                unique: true,
                filter: "\"HardwareId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_devices_LifecycleStatus",
                table: "devices",
                column: "LifecycleStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_HardwareId",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_LifecycleStatus",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "devices");

            migrationBuilder.CreateIndex(
                name: "IX_devices_HardwareId",
                table: "devices",
                column: "HardwareId",
                filter: "\"HardwareId\" IS NOT NULL");
        }
    }
}
