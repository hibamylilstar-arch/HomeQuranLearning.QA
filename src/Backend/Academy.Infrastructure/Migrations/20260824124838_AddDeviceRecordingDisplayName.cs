using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceRecordingDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecordingDisplayName",
                table: "devices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordingDisplayName",
                table: "devices");
        }
    }
}
