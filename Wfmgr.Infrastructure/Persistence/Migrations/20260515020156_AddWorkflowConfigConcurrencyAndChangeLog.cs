using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wfmgr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowConfigConcurrencyAndChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "WorkflowRule",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowRule",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "WorkflowRule",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WorkflowRule",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "WorkflowRule",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WorkflowProfile",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "WorkflowProfile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "WorkflowProfile",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "WorkflowProfile",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "WorkflowConfigChangeLog",
                columns: table => new
                {
                    ChangeLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SnapshotJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowConfigChangeLog", x => x.ChangeLogId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowConfigChangeLog_EntityType_EntityId_CreatedAt",
                table: "WorkflowConfigChangeLog",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowConfigChangeLog_ProfileId_CreatedAt",
                table: "WorkflowConfigChangeLog",
                columns: new[] { "ProfileId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowConfigChangeLog");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "WorkflowRule");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowRule");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WorkflowRule");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WorkflowRule");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "WorkflowRule");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WorkflowProfile");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "WorkflowProfile");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "WorkflowProfile");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "WorkflowProfile");
        }
    }
}
