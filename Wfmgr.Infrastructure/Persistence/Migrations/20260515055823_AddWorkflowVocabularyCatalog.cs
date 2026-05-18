using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wfmgr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowVocabularyCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowVocabularyChangeLog",
                columns: table => new
                {
                    ChangeLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowVocabularyChangeLog", x => x.ChangeLogId);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowVocabularyTerm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowVocabularyTerm", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVocabularyChangeLog_Kind_Code_CreatedAt",
                table: "WorkflowVocabularyChangeLog",
                columns: new[] { "Kind", "Code", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVocabularyChangeLog_TermId_CreatedAt",
                table: "WorkflowVocabularyChangeLog",
                columns: new[] { "TermId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVocabularyTerm_Kind_Code",
                table: "WorkflowVocabularyTerm",
                columns: new[] { "Kind", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowVocabularyTerm_Kind_SortOrder",
                table: "WorkflowVocabularyTerm",
                columns: new[] { "Kind", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowVocabularyChangeLog");

            migrationBuilder.DropTable(
                name: "WorkflowVocabularyTerm");
        }
    }
}
