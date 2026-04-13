using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StaticCodeAnalyzer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisRules",
                columns: table => new
                {
                    RuleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleName = table.Column<string>(type: "text", nullable: false),
                    RuleCategory = table.Column<string>(type: "text", nullable: false),
                    RuleSeverity = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RuleDescription = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRules", x => x.RuleId);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectName = table.Column<string>(type: "text", nullable: false),
                    ProjectPath = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastScanned = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.ProjectId);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisExclusions",
                columns: table => new
                {
                    ExclusionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ExclusionPattern = table.Column<string>(type: "text", nullable: false),
                    ExclusionType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisExclusions", x => x.ExclusionId);
                    table.ForeignKey(
                        name: "FK_AnalysisExclusions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Scans",
                columns: table => new
                {
                    ScanId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Parameters = table.Column<string>(type: "jsonb", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    ExternalLogPath = table.Column<string>(type: "text", nullable: false),
                    TotalFilesScanned = table.Column<int>(type: "integer", nullable: false),
                    TotalIssuesFound = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scans", x => x.ScanId);
                    table.ForeignKey(
                        name: "FK_Scans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisResults",
                columns: table => new
                {
                    ResultId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    RuleId = table.Column<int>(type: "integer", nullable: true),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    ColumnNumber = table.Column<int>(type: "integer", nullable: true),
                    IssueType = table.Column<string>(type: "text", nullable: false),
                    IssueSeverity = table.Column<string>(type: "text", nullable: false),
                    IssueCode = table.Column<string>(type: "text", nullable: false),
                    IssueDescription = table.Column<string>(type: "text", nullable: false),
                    SuggestedFix = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisResults", x => x.ResultId);
                    table.ForeignKey(
                        name: "FK_AnalysisResults_AnalysisRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "AnalysisRules",
                        principalColumn: "RuleId");
                    table.ForeignKey(
                        name: "FK_AnalysisResults_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalysisResults_Scans_ScanId",
                        column: x => x.ScanId,
                        principalTable: "Scans",
                        principalColumn: "ScanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScannedFiles",
                columns: table => new
                {
                    ScannedFileId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanId = table.Column<int>(type: "integer", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    FileExtension = table.Column<string>(type: "text", nullable: false),
                    LinesCount = table.Column<int>(type: "integer", nullable: false),
                    IssuesCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannedFiles", x => x.ScannedFileId);
                    table.ForeignKey(
                        name: "FK_ScannedFiles_Scans_ScanId",
                        column: x => x.ScanId,
                        principalTable: "Scans",
                        principalColumn: "ScanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisExclusions_ProjectId",
                table: "AnalysisExclusions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_FilePath",
                table: "AnalysisResults",
                column: "FilePath");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_ProjectId",
                table: "AnalysisResults",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_RuleId",
                table: "AnalysisResults",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_ScanId",
                table: "AnalysisResults",
                column: "ScanId");

            migrationBuilder.CreateIndex(
                name: "IX_ScannedFiles_ScanId",
                table: "ScannedFiles",
                column: "ScanId");

            migrationBuilder.CreateIndex(
                name: "IX_Scans_ProjectId",
                table: "Scans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Scans_StartTime",
                table: "Scans",
                column: "StartTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisExclusions");

            migrationBuilder.DropTable(
                name: "AnalysisResults");

            migrationBuilder.DropTable(
                name: "ScannedFiles");

            migrationBuilder.DropTable(
                name: "AnalysisRules");

            migrationBuilder.DropTable(
                name: "Scans");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
