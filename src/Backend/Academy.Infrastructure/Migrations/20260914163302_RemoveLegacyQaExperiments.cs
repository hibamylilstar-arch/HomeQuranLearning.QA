using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyQaExperiments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qa_audio_chunks");

            migrationBuilder.DropTable(
                name: "qa_candidates");

            migrationBuilder.DropTable(
                name: "transcript_segments");

            migrationBuilder.DropIndex(
                name: "IX_qa_alerts_SourceQaAudioChunkId",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "QaProcessedAtUtc",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "SourceQaAudioChunkId",
                table: "qa_alerts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QaProcessedAtUtc",
                table: "recordings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceQaAudioChunkId",
                table: "qa_alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "qa_audio_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    BitsPerSample = table.Column<int>(type: "integer", nullable: false),
                    CaptureId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channels = table.Column<int>(type: "integer", nullable: false),
                    ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeleteAfterUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FormatVersion = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SampleRate = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qa_audio_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qa_audio_chunks_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qa_audio_chunks_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qa_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedQaAlertId = table.Column<Guid>(type: "uuid", nullable: true),
                    QaRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualDeviceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AnalysisIdempotencyKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AnalysisVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AsrConfidence = table.Column<double>(type: "double precision", nullable: true),
                    AudioLayoutVersion = table.Column<int>(type: "integer", nullable: false),
                    ContextEndSeconds = table.Column<double>(type: "double precision", nullable: false),
                    ContextStartSeconds = table.Column<double>(type: "double precision", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetectionReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceEndSeconds = table.Column<double>(type: "double precision", nullable: true),
                    EvidenceStartSeconds = table.Column<double>(type: "double precision", nullable: true),
                    IntentCategory = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IntentConfidence = table.Column<double>(type: "double precision", nullable: true),
                    LanguageFamily = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LaptopName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MatchedPhrase = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReviewReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ReviewVersion = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceTrackIndex = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Transcript = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    TriggerConfidence = table.Column<double>(type: "double precision", nullable: true),
                    TriggerEndSeconds = table.Column<double>(type: "double precision", nullable: false),
                    TriggerStartSeconds = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qa_candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qa_candidates_qa_alerts_ConfirmedQaAlertId",
                        column: x => x.ConfirmedQaAlertId,
                        principalTable: "qa_alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_qa_candidates_qa_rules_QaRuleId",
                        column: x => x.QaRuleId,
                        principalTable: "qa_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_qa_candidates_recordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "recordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transcript_segments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordingId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvgLogProbability = table.Column<double>(type: "double precision", nullable: true),
                    CompressionRatio = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NoSpeechProbability = table.Column<double>(type: "double precision", nullable: true),
                    SegmentIndex = table.Column<int>(type: "integer", nullable: false),
                    StartSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false)
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
                name: "IX_qa_alerts_SourceQaAudioChunkId",
                table: "qa_alerts",
                column: "SourceQaAudioChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_qa_audio_chunks_DeleteAfterUtc",
                table: "qa_audio_chunks",
                column: "DeleteAfterUtc");

            migrationBuilder.CreateIndex(
                name: "IX_qa_audio_chunks_DeviceId_SessionId_CaptureId_SequenceNumber",
                table: "qa_audio_chunks",
                columns: new[] { "DeviceId", "SessionId", "CaptureId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_audio_chunks_ProcessedAtUtc_CreatedAtUtc",
                table: "qa_audio_chunks",
                columns: new[] { "ProcessedAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_qa_audio_chunks_SessionId_StartedAtUtc",
                table: "qa_audio_chunks",
                columns: new[] { "SessionId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_qa_candidates_AnalysisIdempotencyKey",
                table: "qa_candidates",
                column: "AnalysisIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_candidates_ConfirmedQaAlertId",
                table: "qa_candidates",
                column: "ConfirmedQaAlertId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_candidates_QaRuleId",
                table: "qa_candidates",
                column: "QaRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_qa_candidates_RecordingId_PolicyVersion_AnalysisVersion_Sou~",
                table: "qa_candidates",
                columns: new[] { "RecordingId", "PolicyVersion", "AnalysisVersion", "SourceTrackIndex", "TriggerStartSeconds", "TriggerEndSeconds" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transcript_segments_RecordingId_SegmentIndex",
                table: "transcript_segments",
                columns: new[] { "RecordingId", "SegmentIndex" },
                unique: true);
        }
    }
}
