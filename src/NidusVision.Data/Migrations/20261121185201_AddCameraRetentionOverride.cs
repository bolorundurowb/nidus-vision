using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NidusVision.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraRetentionOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetentionDaysOverride",
                table: "Cameras",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetentionDaysOverride",
                table: "Cameras");
        }
    }
}
