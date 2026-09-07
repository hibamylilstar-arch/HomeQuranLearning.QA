using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQaCommercialEvidenceProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActualDeviceName",
                table: "qa_candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                table: "qa_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "qa_candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectionReason",
                table: "qa_candidates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "qa_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EvidenceEndSeconds",
                table: "qa_candidates",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EvidenceStartSeconds",
                table: "qa_candidates",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaptopName",
                table: "qa_candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "qa_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                table: "qa_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentName",
                table: "qa_candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "qa_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherName",
                table: "qa_candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MatchedPhrase",
                table: "qa_alerts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AddColumn<string>(
                name: "ActualDeviceName",
                table: "qa_alerts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisIdempotencyKey",
                table: "qa_alerts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisVersion",
                table: "qa_alerts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AudioLayoutVersion",
                table: "qa_alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                table: "qa_alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "qa_alerts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectionReason",
                table: "qa_alerts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "qa_alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EvidenceEndSeconds",
                table: "qa_alerts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EvidenceStartSeconds",
                table: "qa_alerts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaptopName",
                table: "qa_alerts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "qa_alerts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "qa_alerts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewVersion",
                table: "qa_alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAtUtc",
                table: "qa_alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "qa_alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "qa_alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceTrackIndex",
                table: "qa_alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                table: "qa_alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentName",
                table: "qa_alerts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "qa_alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherName",
                table: "qa_alerts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transcript",
                table: "qa_alerts",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TriggerEndSeconds",
                table: "qa_alerts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TriggerStartSeconds",
                table: "qa_alerts",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_qa_alerts_AnalysisIdempotencyKey",
                table: "qa_alerts",
                column: "AnalysisIdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_qa_alerts_AnalysisIdempotencyKey",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "ActualDeviceName",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "CourseName",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "DetectionReason",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "EvidenceEndSeconds",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "EvidenceStartSeconds",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "LaptopName",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "StudentName",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "TeacherName",
                table: "qa_candidates");

            migrationBuilder.DropColumn(
                name: "ActualDeviceName",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "AnalysisIdempotencyKey",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "AnalysisVersion",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "AudioLayoutVersion",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "CourseName",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "DetectionReason",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "EvidenceEndSeconds",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "EvidenceStartSeconds",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "LaptopName",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "ReviewVersion",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "SourceTrackIndex",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "StudentName",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "TeacherName",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "Transcript",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "TriggerEndSeconds",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "TriggerStartSeconds",
                table: "qa_alerts");

            migrationBuilder.AlterColumn<string>(
                name: "MatchedPhrase",
                table: "qa_alerts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);
        }
    }
}
