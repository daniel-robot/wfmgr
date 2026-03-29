using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfmgr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandRadiotherapyWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "WorkItem",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedBy",
                table: "WorkItem",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FormId",
                table: "WorkItem",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentWorkItemId",
                table: "WorkItem",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "WorkItem",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequiresDifferentUserFrom",
                table: "WorkItem",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultCode",
                table: "WorkItem",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "WorkItem",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SequenceNo",
                table: "WorkItem",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkItemGroup",
                table: "WorkItem",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentPlanVersionNo",
                table: "Case",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentPlannerUserId",
                table: "Case",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentReviewerUserId",
                table: "Case",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Case",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CaseAttachment",
                columns: table => new
                {
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UploadedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseAttachment", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_CaseAttachment_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseForm",
                columns: table => new
                {
                    FormId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FormVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    SubmittedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseForm", x => x.FormId);
                    table.ForeignKey(
                        name: "FK_CaseForm_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseTransitionHistory",
                columns: table => new
                {
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TriggerType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TriggerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTransitionHistory", x => x.TransitionId);
                    table.ForeignKey(
                        name: "FK_CaseTransitionHistory_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationReference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalEntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationReference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationReference_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanVersion",
                columns: table => new
                {
                    PlanVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SummaryJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanVersion", x => x.PlanVersionId);
                    table.ForeignKey(
                        name: "FK_PlanVersion_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItem_FormId",
                table: "WorkItem",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItem_ParentWorkItemId",
                table: "WorkItem",
                column: "ParentWorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseAttachment_CaseId",
                table: "CaseAttachment",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseForm_CaseId_FormType",
                table: "CaseForm",
                columns: new[] { "CaseId", "FormType" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseTransitionHistory_CaseId_CreatedAt",
                table: "CaseTransitionHistory",
                columns: new[] { "CaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationReference_CaseId_SystemName",
                table: "IntegrationReference",
                columns: new[] { "CaseId", "SystemName" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersion_CaseId_VersionNo",
                table: "PlanVersion",
                columns: new[] { "CaseId", "VersionNo" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItem_CaseForm_FormId",
                table: "WorkItem",
                column: "FormId",
                principalTable: "CaseForm",
                principalColumn: "FormId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItem_WorkItem_ParentWorkItemId",
                table: "WorkItem",
                column: "ParentWorkItemId",
                principalTable: "WorkItem",
                principalColumn: "WorkItemId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItem_CaseForm_FormId",
                table: "WorkItem");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItem_WorkItem_ParentWorkItemId",
                table: "WorkItem");

            migrationBuilder.DropTable(
                name: "CaseAttachment");

            migrationBuilder.DropTable(
                name: "CaseForm");

            migrationBuilder.DropTable(
                name: "CaseTransitionHistory");

            migrationBuilder.DropTable(
                name: "IntegrationReference");

            migrationBuilder.DropTable(
                name: "PlanVersion");

            migrationBuilder.DropIndex(
                name: "IX_WorkItem_FormId",
                table: "WorkItem");

            migrationBuilder.DropIndex(
                name: "IX_WorkItem_ParentWorkItemId",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "FormId",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "ParentWorkItemId",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "RequiresDifferentUserFrom",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "ResultCode",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "SequenceNo",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "WorkItemGroup",
                table: "WorkItem");

            migrationBuilder.DropColumn(
                name: "CurrentPlanVersionNo",
                table: "Case");

            migrationBuilder.DropColumn(
                name: "CurrentPlannerUserId",
                table: "Case");

            migrationBuilder.DropColumn(
                name: "CurrentReviewerUserId",
                table: "Case");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Case");
        }
    }
}
