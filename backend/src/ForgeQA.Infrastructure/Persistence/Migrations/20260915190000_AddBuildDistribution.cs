using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeQA.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ForgeQADbContext))]
[Migration("20260915190000_AddBuildDistribution")]
public partial class AddBuildDistribution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BuildArtifacts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BuildId = table.Column<Guid>(type: "uuid", nullable: false),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ArtifactType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                StorageObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BuildArtifacts", x => x.Id);
                table.ForeignKey(
                    name: "FK_BuildArtifacts_Builds_BuildId",
                    column: x => x.BuildId,
                    principalTable: "Builds",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ArtifactUploadSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderUploadId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                PartSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                ExpectedParts = table.Column<int>(type: "integer", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AbortedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArtifactUploadSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ArtifactUploadSessions_BuildArtifacts_ArtifactId",
                    column: x => x.ArtifactId,
                    principalTable: "BuildArtifacts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_BuildArtifacts_BuildId", table: "BuildArtifacts", column: "BuildId");
        migrationBuilder.CreateIndex(name: "IX_BuildArtifacts_BuildId_CreatedAt", table: "BuildArtifacts", columns: new[] { "BuildId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_BuildArtifacts_BuildId_Status", table: "BuildArtifacts", columns: new[] { "BuildId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_BuildArtifacts_StorageObjectKey", table: "BuildArtifacts", column: "StorageObjectKey", unique: true);
        migrationBuilder.CreateIndex(name: "IX_ArtifactUploadSessions_ArtifactId", table: "ArtifactUploadSessions", column: "ArtifactId");
        migrationBuilder.CreateIndex(name: "IX_ArtifactUploadSessions_ExpiresAt", table: "ArtifactUploadSessions", column: "ExpiresAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ArtifactUploadSessions");
        migrationBuilder.DropTable(name: "BuildArtifacts");
    }
}
