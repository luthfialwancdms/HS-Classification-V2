using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HsClassifier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportBatchKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ImportBatches",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Btki");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ImportBatches");
        }
    }
}
