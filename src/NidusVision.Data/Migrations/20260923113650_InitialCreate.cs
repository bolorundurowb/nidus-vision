using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NidusVision.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GeneralRetentionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectionRetentionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxStorageBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    InferenceEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SampleFps = table.Column<float>(type: "REAL", nullable: false),
                    ConfidenceThreshold = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cameras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MainRtspUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    SubRtspUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    PasswordProtected = table.Column<string>(type: "TEXT", nullable: true),
                    Transport = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RoiJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cameras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DetectionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Confidence = table.Column<float>(type: "REAL", nullable: false),
                    BoundingBoxJson = table.Column<string>(type: "TEXT", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "TEXT", nullable: true),
                    SegmentIdsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetectionEvents_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecordingSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Codec = table.Column<string>(type: "TEXT", nullable: false),
                    HasHuman = table.Column<bool>(type: "INTEGER", nullable: false),
                    ByteSize = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordingSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordingSegments_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_Name",
                table: "Cameras",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DetectionEvents_CameraId_StartUtc",
                table: "DetectionEvents",
                columns: new[] { "CameraId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DetectionEvents_Confidence",
                table: "DetectionEvents",
                column: "Confidence");

            migrationBuilder.CreateIndex(
                name: "IX_RecordingSegments_CameraId_StartUtc",
                table: "RecordingSegments",
                columns: new[] { "CameraId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordingSegments_HasHuman_EndUtc",
                table: "RecordingSegments",
                columns: new[] { "HasHuman", "EndUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "DetectionEvents");

            migrationBuilder.DropTable(
                name: "LocalUsers");

            migrationBuilder.DropTable(
                name: "RecordingSegments");

            migrationBuilder.DropTable(
                name: "Cameras");
        }
    }
}
