using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NidusVision.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameDetectionEventsToIntervals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "DetectionEvents",
                newName: "DetectionIntervals");

            migrationBuilder.RenameIndex(
                name: "IX_DetectionEvents_Confidence",
                table: "DetectionIntervals",
                newName: "IX_DetectionIntervals_Confidence");

            migrationBuilder.RenameIndex(
                name: "IX_DetectionEvents_CameraId_StartUtc",
                table: "DetectionIntervals",
                newName: "IX_DetectionIntervals_CameraId_StartUtc");

            migrationBuilder.DropColumn(
                name: "ClipPath",
                table: "DetectionIntervals");

            migrationBuilder.DropColumn(
                name: "SegmentIdsJson",
                table: "DetectionIntervals");

            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "DetectionIntervals");

            migrationBuilder.DropColumn(
                name: "RoiJson",
                table: "Cameras");

            migrationBuilder.Sql(
                """UPDATE "Cameras" SET "Location" = CASE WHEN "Location" = 'Exterior' THEN 'Exterior' ELSE 'Interior' END;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClipPath",
                table: "DetectionIntervals",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegmentIdsJson",
                table: "DetectionIntervals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "DetectionIntervals",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoiJson",
                table: "Cameras",
                type: "TEXT",
                nullable: true);

            migrationBuilder.RenameTable(
                name: "DetectionIntervals",
                newName: "DetectionEvents");

            migrationBuilder.RenameIndex(
                name: "IX_DetectionIntervals_Confidence",
                table: "DetectionEvents",
                newName: "IX_DetectionEvents_Confidence");

            migrationBuilder.RenameIndex(
                name: "IX_DetectionIntervals_CameraId_StartUtc",
                table: "DetectionEvents",
                newName: "IX_DetectionEvents_CameraId_StartUtc");
        }
    }
}
