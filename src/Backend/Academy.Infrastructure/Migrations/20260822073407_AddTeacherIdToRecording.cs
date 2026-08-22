using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherIdToRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "recordings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_recordings_TeacherId",
                table: "recordings",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_recordings_teachers_TeacherId",
                table: "recordings",
                column: "TeacherId",
                principalTable: "teachers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_recordings_teachers_TeacherId",
                table: "recordings");

            migrationBuilder.DropIndex(
                name: "IX_recordings_TeacherId",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "recordings");
        }
    }
}
