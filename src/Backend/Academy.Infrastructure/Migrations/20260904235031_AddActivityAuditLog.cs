using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorFullName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EntityDisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Summary = table.Column<string>(type: "character varying(768)", maxLength: 768, nullable: false),
                    ChangesJson = table.Column<string>(type: "jsonb", nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_ActorRole_OccurredAtUtc",
                table: "audit_log_entries",
                columns: new[] { "ActorRole", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_EntityType_OccurredAtUtc",
                table: "audit_log_entries",
                columns: new[] { "EntityType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_OccurredAtUtc",
                table: "audit_log_entries",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entries_RequestId",
                table: "audit_log_entries",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log_entries");
        }
    }
}
