using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HsClassifier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiTariffEstimate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiTariffEstimates",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PpnPercent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    PpnBasis = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Pph22Percent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    Pph22Basis = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FtaSchemes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTariffEstimates", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiTariffEstimates");

            migrationBuilder.DropTable(
                name: "AppSettings");
        }
    }
}
