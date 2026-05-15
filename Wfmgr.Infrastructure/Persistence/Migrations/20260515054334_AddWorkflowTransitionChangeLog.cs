using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wfmgr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTransitionChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowTransitionChangeLog",
                columns: table => new
                {
                    ChangeLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SnapshotJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitionChangeLog", x => x.ChangeLogId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionChangeLog_Code_CreatedAt",
                table: "WorkflowTransitionChangeLog",
                columns: new[] { "Code", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionChangeLog_TransitionId_CreatedAt",
                table: "WorkflowTransitionChangeLog",
                columns: new[] { "TransitionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowTransitionChangeLog");
        }
    }
}
