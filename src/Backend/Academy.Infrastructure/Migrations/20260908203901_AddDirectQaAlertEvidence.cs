using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectQaAlertEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RecordingId",
                table: "qa_alerts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceContentType",
                table: "qa_alerts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EvidenceDeleteAfterUtc",
                table: "qa_alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EvidenceDurationSeconds",
                table: "qa_alerts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EvidenceEndUtc",
                table: "qa_alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EvidenceSizeBytes",
                table: "qa_alerts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EvidenceStartUtc",
                table: "qa_alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceStorageKey",
                table: "qa_alerts",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceQaAudioChunkId",
                table: "qa_alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_alerts_EvidenceDeleteAfterUtc",
                table: "qa_alerts",
                column: "EvidenceDeleteAfterUtc");

            migrationBuilder.CreateIndex(
                name: "IX_qa_alerts_SourceQaAudioChunkId",
                table: "qa_alerts",
                column: "SourceQaAudioChunkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_qa_alerts_EvidenceDeleteAfterUtc",
                table: "qa_alerts");

            migrationBuilder.DropIndex(
                name: "IX_qa_alerts_SourceQaAudioChunkId",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "EvidenceContentType",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "EvidenceDeleteAfterUtc",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "EvidenceDurationSeconds",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "EvidenceEndUtc",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "EvidenceSizeBytes",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "EvidenceStartUtc",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "EvidenceStorageKey",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "SourceQaAudioChunkId",
                table: "qa_alerts");

            migrationBuilder.AlterColumn<Guid>(
                name: "RecordingId",
                table: "qa_alerts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
