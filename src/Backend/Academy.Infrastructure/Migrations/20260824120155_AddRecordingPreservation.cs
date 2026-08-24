using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingPreservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPreserved",
                table: "recordings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreservedAtUtc",
                table: "recordings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPreserved",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "PreservedAtUtc",
                table: "recordings");
        }
    }
}
