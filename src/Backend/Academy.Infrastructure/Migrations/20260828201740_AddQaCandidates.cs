using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQaCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "qa_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordingId = table.Column<Guid>(type: "uuid", nullable: false),
                    QaRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedQaAlertId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AnalysisVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceTrackIndex = table.Column<int>(type: "integer", nullable: false),
                    AudioLayoutVersion = table.Column<int>(type: "integer", nullable: false),
                    TriggerStartSeconds = table.Column<double>(type: "double precision", nullable: false),
                    TriggerEndSeconds = table.Column<double>(type: "double precision", nullable: false),
                    ContextStartSeconds = table.Column<double>(type: "double precision", nullable: false),
                    ContextEndSeconds = table.Column<double>(type: "double precision", nullable: false),
                    Transcript = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    LanguageFamily = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IntentCategory = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TriggerConfidence = table.Column<double>(type: "double precision", nullable: true),
                    AsrConfidence = table.Column<double>(type: "double precision", nullable: true),
                    IntentConfidence = table.Column<double>(type: "double precision", nullable: true),
                    AnalysisIdempotencyKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ReviewVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qa_candidates");
        }
    }
}
