using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeQA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelemetrySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuntimeSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastEventAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Environment_MapName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Environment_GameMode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Environment_Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Environment_Configuration = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Environment_EngineVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Environment_OsVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Environment_Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetrySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetrySessions_Builds_BuildId",
                        column: x => x.BuildId,
                        principalTable: "Builds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelemetrySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    EventName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClientTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Properties = table.Column<string>(type: "jsonb", nullable: false),
                    MapName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetryEvents_TelemetrySessions_TelemetrySessionId",
                        column: x => x.TelemetrySessionId,
                        principalTable: "TelemetrySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryEvents_TelemetrySessionId_ClientTimestamp",
                table: "TelemetryEvents",
                columns: new[] { "TelemetrySessionId", "ClientTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryEvents_TelemetrySessionId_EventName",
                table: "TelemetryEvents",
                columns: new[] { "TelemetrySessionId", "EventName" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryEvents_TelemetrySessionId_SequenceNumber",
                table: "TelemetryEvents",
                columns: new[] { "TelemetrySessionId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetrySessions_BuildId",
                table: "TelemetrySessions",
                column: "BuildId");

            migrationBuilder.CreateIndex(
                name: "IX_TelemetrySessions_ProjectId_BuildId",
                table: "TelemetrySessions",
                columns: new[] { "ProjectId", "BuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetrySessions_ProjectId_RuntimeSessionId",
                table: "TelemetrySessions",
                columns: new[] { "ProjectId", "RuntimeSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetrySessions_ProjectId_StartedAt",
                table: "TelemetrySessions",
                columns: new[] { "ProjectId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelemetryEvents");

            migrationBuilder.DropTable(
                name: "TelemetrySessions");
        }
    }
}
