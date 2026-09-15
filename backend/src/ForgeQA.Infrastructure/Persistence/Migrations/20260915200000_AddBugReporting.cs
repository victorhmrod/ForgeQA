using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeQA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBugReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.CreateTable(
                name: "BugReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ReproductionSteps = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReporterDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RuntimeSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Environment_MapName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Environment_GameMode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Environment_Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Environment_EngineVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Environment_OsVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Environment_Cpu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Environment_Gpu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Environment_MemoryBytes = table.Column<long>(type: "bigint", nullable: true),
                    Environment_Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BugReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BugReports_Builds_BuildId",
                        column: x => x.BuildId,
                        principalTable: "Builds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Scopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BugAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BugReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BugAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BugAttachments_BugReports_BugReportId",
                        column: x => x.BugReportId,
                        principalTable: "BugReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BugAttachments_BugReportId",
                table: "BugAttachments",
                column: "BugReportId");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_BuildId",
                table: "BugReports",
                column: "BuildId");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_ProjectId_BuildId",
                table: "BugReports",
                columns: new[] { "ProjectId", "BuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_ProjectId_CreatedAt",
                table: "BugReports",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_ProjectId_Severity",
                table: "BugReports",
                columns: new[] { "ProjectId", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_ProjectId_Source",
                table: "BugReports",
                columns: new[] { "ProjectId", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_ProjectId_Status",
                table: "BugReports",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectApiKeys_Prefix",
                table: "ProjectApiKeys",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectApiKeys_ProjectId",
                table: "ProjectApiKeys",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BugAttachments");

            migrationBuilder.DropTable(
                name: "ProjectApiKeys");

            migrationBuilder.DropTable(
                name: "BugReports");

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ProjectId",
                table: "Reports",
                column: "ProjectId");
        }
    }
}
