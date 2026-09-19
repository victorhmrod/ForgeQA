using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeQA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerformanceSamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelemetrySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    ClientTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MapName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Fps = table.Column<double>(type: "double precision", nullable: false),
                    FrameTimeMs = table.Column<double>(type: "double precision", nullable: false),
                    GameThreadTimeMs = table.Column<double>(type: "double precision", nullable: true),
                    RenderThreadTimeMs = table.Column<double>(type: "double precision", nullable: true),
                    GpuTimeMs = table.Column<double>(type: "double precision", nullable: true),
                    MemoryUsedBytes = table.Column<long>(type: "bigint", nullable: true),
                    MemoryAvailableBytes = table.Column<long>(type: "bigint", nullable: true),
                    CpuUtilizationPercent = table.Column<double>(type: "double precision", nullable: true),
                    GpuUtilizationPercent = table.Column<double>(type: "double precision", nullable: true),
                    DrawCalls = table.Column<int>(type: "integer", nullable: true),
                    PlayerCount = table.Column<int>(type: "integer", nullable: true),
                    PingMs = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceSamples_TelemetrySessions_TelemetrySessionId",
                        column: x => x.TelemetrySessionId,
                        principalTable: "TelemetrySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceSamples_TelemetrySessionId_ClientTimestamp",
                table: "PerformanceSamples",
                columns: new[] { "TelemetrySessionId", "ClientTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceSamples_TelemetrySessionId_MapName",
                table: "PerformanceSamples",
                columns: new[] { "TelemetrySessionId", "MapName" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceSamples_TelemetrySessionId_SequenceNumber",
                table: "PerformanceSamples",
                columns: new[] { "TelemetrySessionId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerformanceSamples");
        }
    }
}
