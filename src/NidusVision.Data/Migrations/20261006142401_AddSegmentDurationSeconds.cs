using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NidusVision.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSegmentDurationSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SegmentDurationSeconds",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 900);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SegmentDurationSeconds",
                table: "AppSettings");
        }
    }
}
