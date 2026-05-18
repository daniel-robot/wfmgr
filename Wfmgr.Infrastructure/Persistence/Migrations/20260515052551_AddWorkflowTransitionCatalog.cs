using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfmgr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTransitionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowTransition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Phase = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TriggerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TriggerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConfigSlot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitionAttribute",
                columns: table => new
                {
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitionAttribute", x => new { x.TransitionId, x.Kind, x.Value });
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionAttribute_WorkflowTransition_TransitionId",
                        column: x => x.TransitionId,
                        principalTable: "WorkflowTransition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitionFromStatus",
                columns: table => new
                {
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitionFromStatus", x => new { x.TransitionId, x.FromStatus });
                    table.ForeignKey(
                        name: "FK_WorkflowTransitionFromStatus_WorkflowTransition_TransitionId",
                        column: x => x.TransitionId,
                        principalTable: "WorkflowTransition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_Code",
                table: "WorkflowTransition",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_Phase_SortOrder",
                table: "WorkflowTransition",
                columns: new[] { "Phase", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowTransitionAttribute");

            migrationBuilder.DropTable(
                name: "WorkflowTransitionFromStatus");

            migrationBuilder.DropTable(
                name: "WorkflowTransition");
        }
    }
}
