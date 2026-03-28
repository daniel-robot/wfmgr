using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfmgr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Case",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    HospitalId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SiteId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DepartmentId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PatientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AccessionNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrentStatus = table.Column<int>(type: "integer", nullable: false),
                    StatusVersion = table.Column<int>(type: "integer", nullable: false),
                    CtStudyInstanceUid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CtWadoRsUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PvMedJobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RtStructSeriesInstanceUid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Case", x => x.CaseId);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowProfile",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    HospitalId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SiteId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DepartmentId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowProfile", x => x.ProfileId);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: true),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.AuditId);
                    table.ForeignKey(
                        name: "FK_AuditLog_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalEvent",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CaseCorrelationKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalEvent", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_ExternalEvent_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastTriedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkItem",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssignedUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SlaMinutes = table.Column<int>(type: "integer", nullable: true),
                    ExternalCorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItem", x => x.WorkItemId);
                    table.ForeignKey(
                        name: "FK_WorkItem_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRule",
                columns: table => new
                {
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ConditionJson = table.Column<string>(type: "text", nullable: true),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRule", x => x.RuleId);
                    table.ForeignKey(
                        name: "FK_WorkflowRule_WorkflowProfile_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "WorkflowProfile",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CaseId_CreatedAt",
                table: "AuditLog",
                columns: new[] { "CaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Case_CurrentStatus",
                table: "Case",
                column: "CurrentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Case_HospitalId_SiteId_DepartmentId_AccessionNumber",
                table: "Case",
                columns: new[] { "HospitalId", "SiteId", "DepartmentId", "AccessionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalEvent_CaseId",
                table: "ExternalEvent",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalEvent_Source_Type_ExternalId",
                table: "ExternalEvent",
                columns: new[] { "Source", "Type", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_CaseId",
                table: "OutboxMessage",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Status_NextRetryAt",
                table: "OutboxMessage",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowProfile_HospitalId_SiteId_DepartmentId_Version",
                table: "WorkflowProfile",
                columns: new[] { "HospitalId", "SiteId", "DepartmentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRule_ProfileId_SlotCode_IsEnabled",
                table: "WorkflowRule",
                columns: new[] { "ProfileId", "SlotCode", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRule_ProfileId_SlotCode_Priority",
                table: "WorkflowRule",
                columns: new[] { "ProfileId", "SlotCode", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItem_AssignedRole_Status",
                table: "WorkItem",
                columns: new[] { "AssignedRole", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItem_CaseId",
                table: "WorkItem",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItem_Status",
                table: "WorkItem",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "ExternalEvent");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "WorkflowRule");

            migrationBuilder.DropTable(
                name: "WorkItem");

            migrationBuilder.DropTable(
                name: "WorkflowProfile");

            migrationBuilder.DropTable(
                name: "Case");
        }
    }
}
