using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NidusVision.Data;

#nullable disable

namespace NidusVision.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260925143000_AddRecordingSegmentFinalized")]
    public partial class AddRecordingSegmentFinalized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFinalized",
                table: "RecordingSegments",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordingSegments_IsFinalized",
                table: "RecordingSegments",
                column: "IsFinalized");

            migrationBuilder.CreateIndex(
                name: "IX_RecordingSegments_Path",
                table: "RecordingSegments",
                column: "Path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecordingSegments_Path",
                table: "RecordingSegments");

            migrationBuilder.DropIndex(
                name: "IX_RecordingSegments_IsFinalized",
                table: "RecordingSegments");

            migrationBuilder.DropColumn(
                name: "IsFinalized",
                table: "RecordingSegments");
        }
    }
}
