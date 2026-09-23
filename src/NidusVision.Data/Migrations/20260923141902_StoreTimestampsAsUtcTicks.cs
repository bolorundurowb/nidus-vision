using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NidusVision.Data.Migrations
{
    /// <inheritdoc />
    public partial class StoreTimestampsAsUtcTicks : Migration
    {
        private static readonly (string Table, string Column)[] Timestamps =
        [
            ("RecordingSegments", "StartUtc"),
            ("RecordingSegments", "EndUtc"),
            ("DetectionEvents", "StartUtc"),
            ("DetectionEvents", "EndUtc"),
            ("Cameras", "CreatedAt"),
            ("Cameras", "UpdatedAt"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rewrite the ISO-8601 text written by the previous schema into UTC ticks before the
            // column type changes; the table rebuild copies values verbatim and cannot convert them.
            foreach (var (table, column) in Timestamps)
            {
                migrationBuilder.Sql(
                    $"""
                     UPDATE "{table}"
                     SET "{column}" = CAST(ROUND((julianday("{column}") - 2440587.5) * 86400000) AS INTEGER) * 10000 + 621355968000000000
                     WHERE typeof("{column}") = 'text';
                     """);
            }

            migrationBuilder.AlterColumn<long>(
                name: "StartUtc",
                table: "RecordingSegments",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "EndUtc",
                table: "RecordingSegments",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "StartUtc",
                table: "DetectionEvents",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "EndUtc",
                table: "DetectionEvents",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "UpdatedAt",
                table: "Cameras",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "CreatedAt",
                table: "Cameras",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartUtc",
                table: "RecordingSegments",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndUtc",
                table: "RecordingSegments",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartUtc",
                table: "DetectionEvents",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndUtc",
                table: "DetectionEvents",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Cameras",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Cameras",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            foreach (var (table, column) in Timestamps)
            {
                migrationBuilder.Sql(
                    $"""
                     UPDATE "{table}"
                     SET "{column}" = strftime('%Y-%m-%d %H:%M:%f', 2440587.5 + (CAST("{column}" AS INTEGER) - 621355968000000000) / 864000000000.0) || '0000+00:00'
                     WHERE typeof("{column}") = 'integer';
                     """);
            }
        }
    }
}
