using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGateNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OpenedAt",
                table: "gates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_device_notification_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotifyOnOpen = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnClose = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyWhenOpenTooLong = table.Column<bool>(type: "boolean", nullable: false),
                    OpenAlertMinutes = table.Column<int>(type: "integer", nullable: false),
                    LastOpenAlertAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_device_notification_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_device_notification_preferences_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_device_notification_preferences_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_device_notification_preferences_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gates_GateState_OpenedAt",
                table: "gates",
                columns: new[] { "GateState", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_device_notification_preferences_DeviceId",
                table: "user_device_notification_preferences",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_user_device_notification_preferences_TenantId",
                table: "user_device_notification_preferences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_user_device_notification_preferences_UserId_DeviceId",
                table: "user_device_notification_preferences",
                columns: new[] { "UserId", "DeviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_device_notification_preferences");

            migrationBuilder.DropIndex(
                name: "IX_gates_GateState_OpenedAt",
                table: "gates");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "gates");
        }
    }
}
