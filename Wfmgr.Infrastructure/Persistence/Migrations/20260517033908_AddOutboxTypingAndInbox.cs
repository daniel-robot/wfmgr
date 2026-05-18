using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfmgr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxTypingAndInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "OutboxMessage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryMode",
                table: "OutboxMessage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "OutboxMessage",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "OutboxMessage",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Traceparent",
                table: "OutboxMessage",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalEventInbox",
                columns: table => new
                {
                    Integration = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Traceparent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalEventInbox", x => new { x.Integration, x.ExternalEventId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_CorrelationId",
                table: "OutboxMessage",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalEventInbox_CaseId",
                table: "ExternalEventInbox",
                column: "CaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalEventInbox");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_CorrelationId",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "DeliveryMode",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "Traceparent",
                table: "OutboxMessage");
        }
    }
}
