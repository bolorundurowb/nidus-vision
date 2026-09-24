using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NidusVision.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingThumbnailPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "RecordingSegments",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "RecordingSegments");
        }
    }
}
