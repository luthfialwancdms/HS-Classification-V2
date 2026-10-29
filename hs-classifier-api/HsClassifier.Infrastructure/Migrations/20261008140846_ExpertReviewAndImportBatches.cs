using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HsClassifier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpertReviewAndImportBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CandidateCodesJson",
                table: "ClassificationCases",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpertCorrections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BeforeCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    AfterCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    BeforeStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AfterStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewerId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpertCorrections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalNodes = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AmbiguityItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    ProposedParent = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CorrectedParent = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecidedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmbiguityItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AmbiguityItems_ImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmbiguityItems_BatchId_Decision",
                table: "AmbiguityItems",
                columns: new[] { "BatchId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpertCorrections_CaseId",
                table: "ExpertCorrections",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_VersionId",
                table: "ImportBatches",
                column: "VersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmbiguityItems");

            migrationBuilder.DropTable(
                name: "ExpertCorrections");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "CandidateCodesJson",
                table: "ClassificationCases");
        }
    }
}
