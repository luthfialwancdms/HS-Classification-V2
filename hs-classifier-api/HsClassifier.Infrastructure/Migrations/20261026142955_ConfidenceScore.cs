using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HsClassifier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfidenceScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfidenceJson",
                table: "ClassificationCases",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfidenceScore",
                table: "ClassificationCases",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfidenceJson",
                table: "ClassificationCases");

            migrationBuilder.DropColumn(
                name: "ConfidenceScore",
                table: "ClassificationCases");
        }
    }
}
