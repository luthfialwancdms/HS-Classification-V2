using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HsClassifier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClassificationCases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    FinalDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClassificationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NomenclatureVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HsEdition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BtkiEdition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RegulationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NomenclatureVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReasoningEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<long>(type: "bigint", nullable: false),
                    EventOrder = table.Column<int>(type: "int", nullable: false),
                    RuleApplied = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FactsUsedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CandidatesBeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CandidatesAfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reasoning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReasoningEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReasoningEvents_ClassificationCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ClassificationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BtkiTariffLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ParentCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    SectionCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ChapterCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    HeadingCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SubheadingCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    IndentLevel = table.Column<int>(type: "int", nullable: false),
                    DescriptionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IngestionMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BtkiTariffLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BtkiTariffLines_NomenclatureVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "NomenclatureVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChapterNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChapterNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TargetHeading = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    NoteNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ParagraphNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TextId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferredCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NomenclatureVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChapterNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChapterNotes_NomenclatureVersions_NomenclatureVersionId",
                        column: x => x.NomenclatureVersionId,
                        principalTable: "NomenclatureVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExplanatoryNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeadingCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SubheadingCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ParagraphNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParagraphType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TextContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousParagraphId = table.Column<long>(type: "bigint", nullable: true),
                    NextParagraphId = table.Column<long>(type: "bigint", nullable: true),
                    ParentSectionId = table.Column<long>(type: "bigint", nullable: true),
                    ReferredCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NomenclatureVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExplanatoryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExplanatoryNotes_NomenclatureVersions_NomenclatureVersionId",
                        column: x => x.NomenclatureVersionId,
                        principalTable: "NomenclatureVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SectionNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NoteNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ParagraphNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TextId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferredCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NomenclatureVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionNotes_NomenclatureVersions_NomenclatureVersionId",
                        column: x => x.NomenclatureVersionId,
                        principalTable: "NomenclatureVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SubheadingNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeadingCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NoteNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TextId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferredCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NomenclatureVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubheadingNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubheadingNotes_NomenclatureVersions_NomenclatureVersionId",
                        column: x => x.NomenclatureVersionId,
                        principalTable: "NomenclatureVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FtaRates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TariffLineId = table.Column<long>(type: "bigint", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemeCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PartnerCountryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RateValue = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    RateUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionsSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FtaRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FtaRates_BtkiTariffLines_TariffLineId",
                        column: x => x.TariffLineId,
                        principalTable: "BtkiTariffLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MfnRates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TariffLineId = table.Column<long>(type: "bigint", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateValue = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    RateUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Formula = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfnRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MfnRates_BtkiTariffLines_TariffLineId",
                        column: x => x.TariffLineId,
                        principalTable: "BtkiTariffLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxRates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TariffLineId = table.Column<long>(type: "bigint", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RateValue = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    RateUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRates_BtkiTariffLines_TariffLineId",
                        column: x => x.TariffLineId,
                        principalTable: "BtkiTariffLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BtkiTariffLines_ChapterCode",
                table: "BtkiTariffLines",
                column: "ChapterCode");

            migrationBuilder.CreateIndex(
                name: "IX_BtkiTariffLines_Code_VersionId",
                table: "BtkiTariffLines",
                columns: new[] { "Code", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_BtkiTariffLines_IsActive_VersionId",
                table: "BtkiTariffLines",
                columns: new[] { "IsActive", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_BtkiTariffLines_ParentCode",
                table: "BtkiTariffLines",
                column: "ParentCode");

            migrationBuilder.CreateIndex(
                name: "IX_BtkiTariffLines_VersionId",
                table: "BtkiTariffLines",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChapterNotes_ChapterNumber_VersionId",
                table: "ChapterNotes",
                columns: new[] { "ChapterNumber", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChapterNotes_NomenclatureVersionId",
                table: "ChapterNotes",
                column: "NomenclatureVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChapterNotes_TargetHeading",
                table: "ChapterNotes",
                column: "TargetHeading");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationCases_FinalCode",
                table: "ClassificationCases",
                column: "FinalCode");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationCases_UserId_CreatedAt",
                table: "ClassificationCases",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExplanatoryNotes_HeadingCode_VersionId",
                table: "ExplanatoryNotes",
                columns: new[] { "HeadingCode", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExplanatoryNotes_NomenclatureVersionId",
                table: "ExplanatoryNotes",
                column: "NomenclatureVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExplanatoryNotes_ParagraphType",
                table: "ExplanatoryNotes",
                column: "ParagraphType");

            migrationBuilder.CreateIndex(
                name: "IX_FtaRates_TariffLineId_SchemeCode",
                table: "FtaRates",
                columns: new[] { "TariffLineId", "SchemeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_MfnRates_TariffLineId_VersionId_IsActive",
                table: "MfnRates",
                columns: new[] { "TariffLineId", "VersionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ReasoningEvents_CaseId_EventOrder",
                table: "ReasoningEvents",
                columns: new[] { "CaseId", "EventOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SectionNotes_NomenclatureVersionId",
                table: "SectionNotes",
                column: "NomenclatureVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionNotes_SectionNumber_VersionId",
                table: "SectionNotes",
                columns: new[] { "SectionNumber", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubheadingNotes_HeadingCode_VersionId",
                table: "SubheadingNotes",
                columns: new[] { "HeadingCode", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubheadingNotes_NomenclatureVersionId",
                table: "SubheadingNotes",
                column: "NomenclatureVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_TariffLineId_TaxType_IsActive",
                table: "TaxRates",
                columns: new[] { "TariffLineId", "TaxType", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "ChapterNotes");

            migrationBuilder.DropTable(
                name: "ExplanatoryNotes");

            migrationBuilder.DropTable(
                name: "FtaRates");

            migrationBuilder.DropTable(
                name: "MfnRates");

            migrationBuilder.DropTable(
                name: "ReasoningEvents");

            migrationBuilder.DropTable(
                name: "SectionNotes");

            migrationBuilder.DropTable(
                name: "SubheadingNotes");

            migrationBuilder.DropTable(
                name: "TaxRates");

            migrationBuilder.DropTable(
                name: "ClassificationCases");

            migrationBuilder.DropTable(
                name: "BtkiTariffLines");

            migrationBuilder.DropTable(
                name: "NomenclatureVersions");
        }
    }
}
