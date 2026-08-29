using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscriptSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transcript_segments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentIndex = table.Column<int>(type: "integer", nullable: false),
                    StartSeconds = table.Column<double>(type: "double precision", nullable: false),
                    EndSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AvgLogProbability = table.Column<double>(type: "double precision", nullable: true),
                    NoSpeechProbability = table.Column<double>(type: "double precision", nullable: true),
                    CompressionRatio = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transcript_segments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transcript_segments_recordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "recordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transcript_segments_RecordingId_SegmentIndex",
                table: "transcript_segments",
                columns: new[] { "RecordingId", "SegmentIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transcript_segments");
        }
    }
}
