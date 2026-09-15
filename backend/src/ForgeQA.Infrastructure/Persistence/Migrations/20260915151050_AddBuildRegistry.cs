using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgeQA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CommitHash",
                table: "Builds",
                newName: "CommitSha");

            migrationBuilder.AlterColumn<string>(
                name: "Platform",
                table: "Builds",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Configuration",
                table: "Builds",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Builds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildNumber",
                table: "Builds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Changelog",
                table: "Builds",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByDisplayName",
                table: "Builds",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Builds",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Builds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Builds",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Builds_ProjectId_ArchivedAt",
                table: "Builds",
                columns: new[] { "ProjectId", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Builds_ProjectId_BuildNumber_Platform_Configuration",
                table: "Builds",
                columns: new[] { "ProjectId", "BuildNumber", "Platform", "Configuration" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Builds_ProjectId_Configuration",
                table: "Builds",
                columns: new[] { "ProjectId", "Configuration" });

            migrationBuilder.CreateIndex(
                name: "IX_Builds_ProjectId_CreatedAt",
                table: "Builds",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Builds_ProjectId_Platform",
                table: "Builds",
                columns: new[] { "ProjectId", "Platform" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Builds_ProjectId_ArchivedAt",
                table: "Builds");

            migrationBuilder.DropIndex(
                name: "IX_Builds_ProjectId_BuildNumber_Platform_Configuration",
                table: "Builds");

            migrationBuilder.DropIndex(
                name: "IX_Builds_ProjectId_Configuration",
                table: "Builds");

            migrationBuilder.DropIndex(
                name: "IX_Builds_ProjectId_CreatedAt",
                table: "Builds");

            migrationBuilder.DropIndex(
                name: "IX_Builds_ProjectId_Platform",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "BuildNumber",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "Changelog",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "CreatedByDisplayName",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Builds");

            migrationBuilder.RenameColumn(
                name: "CommitSha",
                table: "Builds",
                newName: "CommitHash");

            migrationBuilder.AlterColumn<string>(
                name: "Platform",
                table: "Builds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Configuration",
                table: "Builds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }
    }
}
