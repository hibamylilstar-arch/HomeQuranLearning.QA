using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQaV3VerificationTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CandidateConfidence",
                table: "qa_alerts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VerifierConfidence",
                table: "qa_alerts",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CandidateConfidence",
                table: "qa_alerts");

            migrationBuilder.DropColumn(
                name: "VerifierConfidence",
                table: "qa_alerts");
        }
    }
}
