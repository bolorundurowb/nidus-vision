using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NidusVision.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraRecordingEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecordingEnabled",
                table: "Cameras",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordingEnabled",
                table: "Cameras");
        }
    }
}
