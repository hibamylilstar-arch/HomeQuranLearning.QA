using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherAudioProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudioLayoutVersion",
                table: "recordings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TeacherAudioCoverageStartedAtUtc",
                table: "recordings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherAudioEndpointId",
                table: "recordings",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherAudioEndpointName",
                table: "recordings",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherAudioProvenanceStatus",
                table: "recordings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "LegacyUnknown");

            migrationBuilder.AddColumn<string>(
                name: "TeacherAudioSourceKind",
                table: "recordings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.AddColumn<int>(
                name: "TeacherAudioTrackIndex",
                table: "recordings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recording_audio_coverage_gaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordingId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recording_audio_coverage_gaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recording_audio_coverage_gaps_recordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "recordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recording_audio_coverage_gaps_RecordingId_StartedAtUtc",
                table: "recording_audio_coverage_gaps",
                columns: new[] { "RecordingId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recording_audio_coverage_gaps");

            migrationBuilder.DropColumn(
                name: "AudioLayoutVersion",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "TeacherAudioCoverageStartedAtUtc",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "TeacherAudioEndpointId",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "TeacherAudioEndpointName",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "TeacherAudioProvenanceStatus",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "TeacherAudioSourceKind",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "TeacherAudioTrackIndex",
                table: "recordings");
        }
    }
}
