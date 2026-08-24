using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceClassWindowFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveSeconds",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ActualDeviceId",
                table: "sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualSessionEndUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualSessionStartUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActualTeacherId",
                table: "sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttendanceNotes",
                table: "sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttendanceReviewStatus",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DisconnectCount",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DisconnectSeconds",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstContactAtUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledEndUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledStartUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Backfill existing historical sessions with their real window.
            migrationBuilder.Sql(@"
                UPDATE sessions
                SET ""ScheduledStartUtc"" = ""StartedAtUtc"",
                    ""ScheduledEndUtc"" = COALESCE(
                        ""EndedAtUtc"",
                        ""StartedAtUtc"" + INTERVAL '30 minutes'
                    );
            ");

            // Remove the temporary year-0001 defaults for future inserts.
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ScheduledStartUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ScheduledEndUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "StudentAttendanceStatus",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeacherAttendanceStatus",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TeacherReadyAtUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveFromUtc",
                table: "schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveToUtc",
                table: "schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "session_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Details = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_events_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_events_IdempotencyKey",
                table: "session_events",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_events_SessionId_OccurredAtUtc",
                table: "session_events",
                columns: new[] { "SessionId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_events");

            migrationBuilder.DropColumn(
                name: "ActiveSeconds",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "ActualDeviceId",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "ActualSessionEndUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "ActualSessionStartUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "ActualTeacherId",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "AttendanceNotes",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "AttendanceReviewStatus",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "DisconnectCount",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "DisconnectSeconds",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "FirstContactAtUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "ScheduledEndUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "ScheduledStartUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "StudentAttendanceStatus",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "TeacherAttendanceStatus",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "TeacherReadyAtUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "EffectiveFromUtc",
                table: "schedules");

            migrationBuilder.DropColumn(
                name: "EffectiveToUtc",
                table: "schedules");
        }
    }
}