using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FoundationHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_Email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_tenants_Slug",
                table: "tenants");

            migrationBuilder.CreateIndex(
                name: "IX_users_CreatedAt",
                table: "users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_CreatedAt",
                table: "tenants",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_CreatedAt",
                table: "tenant_memberships",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_UserId",
                table: "tenant_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_logs_CreatedAt",
                table: "security_audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_logs_TenantId",
                table: "security_audit_logs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_logs_UserId",
                table: "security_audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_residences_CreatedAt",
                table: "residences",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_residences_TenantId_CreatedAt",
                table: "residences",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_residence_memberships_CreatedAt",
                table: "residence_memberships",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_residence_memberships_UserId",
                table: "residence_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_CreatedAt",
                table: "refresh_tokens",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_CreatedAt",
                table: "notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_TenantId",
                table: "notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId",
                table: "notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_ReadAt",
                table: "notifications",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_devices_CreatedAt",
                table: "devices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_devices_TenantId",
                table: "devices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_devices_TenantId_CreatedAt",
                table: "devices",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_device_provisionings_CreatedAt",
                table: "device_provisionings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_device_provisionings_ProvisioningCodeHash",
                table: "device_provisionings",
                column: "ProvisioningCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_provisionings_TenantId",
                table: "device_provisionings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_device_permissions_UserId",
                table: "device_permissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_device_events_CreatedAt",
                table: "device_events",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_device_events_TenantId",
                table: "device_events",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_device_events_TenantId_CreatedAt",
                table: "device_events",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_commands_CreatedAt",
                table: "commands",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_commands_DeviceId",
                table: "commands",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_commands_TenantId",
                table: "commands",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_commands_TenantId_CreatedAt",
                table: "commands",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_commands_tenants_TenantId",
                table: "commands",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_device_events_tenants_TenantId",
                table: "device_events",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_device_provisionings_tenants_TenantId",
                table: "device_provisionings",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_tenants_TenantId",
                table: "devices",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_tenants_TenantId",
                table: "notifications",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_commands_tenants_TenantId",
                table: "commands");

            migrationBuilder.DropForeignKey(
                name: "FK_device_events_tenants_TenantId",
                table: "device_events");

            migrationBuilder.DropForeignKey(
                name: "FK_device_provisionings_tenants_TenantId",
                table: "device_provisionings");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_tenants_TenantId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_tenants_TenantId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_users_CreatedAt",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_Email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_tenants_CreatedAt",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_Slug",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenant_memberships_CreatedAt",
                table: "tenant_memberships");

            migrationBuilder.DropIndex(
                name: "IX_tenant_memberships_UserId",
                table: "tenant_memberships");

            migrationBuilder.DropIndex(
                name: "IX_security_audit_logs_CreatedAt",
                table: "security_audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_security_audit_logs_TenantId",
                table: "security_audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_security_audit_logs_UserId",
                table: "security_audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_residences_CreatedAt",
                table: "residences");

            migrationBuilder.DropIndex(
                name: "IX_residences_TenantId_CreatedAt",
                table: "residences");

            migrationBuilder.DropIndex(
                name: "IX_residence_memberships_CreatedAt",
                table: "residence_memberships");

            migrationBuilder.DropIndex(
                name: "IX_residence_memberships_UserId",
                table: "residence_memberships");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_CreatedAt",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_notifications_CreatedAt",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_TenantId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_UserId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_UserId_ReadAt",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_devices_CreatedAt",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_TenantId",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_TenantId_CreatedAt",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_device_provisionings_CreatedAt",
                table: "device_provisionings");

            migrationBuilder.DropIndex(
                name: "IX_device_provisionings_ProvisioningCodeHash",
                table: "device_provisionings");

            migrationBuilder.DropIndex(
                name: "IX_device_provisionings_TenantId",
                table: "device_provisionings");

            migrationBuilder.DropIndex(
                name: "IX_device_permissions_UserId",
                table: "device_permissions");

            migrationBuilder.DropIndex(
                name: "IX_device_events_CreatedAt",
                table: "device_events");

            migrationBuilder.DropIndex(
                name: "IX_device_events_TenantId",
                table: "device_events");

            migrationBuilder.DropIndex(
                name: "IX_device_events_TenantId_CreatedAt",
                table: "device_events");

            migrationBuilder.DropIndex(
                name: "IX_commands_CreatedAt",
                table: "commands");

            migrationBuilder.DropIndex(
                name: "IX_commands_DeviceId",
                table: "commands");

            migrationBuilder.DropIndex(
                name: "IX_commands_TenantId",
                table: "commands");

            migrationBuilder.DropIndex(
                name: "IX_commands_TenantId_CreatedAt",
                table: "commands");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);
        }
    }
}
