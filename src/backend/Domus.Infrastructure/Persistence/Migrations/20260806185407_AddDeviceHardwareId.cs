using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceHardwareId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HardwareId",
                table: "devices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_HardwareId",
                table: "devices",
                column: "HardwareId",
                filter: "\"HardwareId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_HardwareId",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "HardwareId",
                table: "devices");
        }
    }
}
