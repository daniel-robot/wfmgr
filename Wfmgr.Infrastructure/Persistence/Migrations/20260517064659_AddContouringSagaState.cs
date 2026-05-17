using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfmgr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContouringSagaState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContouringSagaState",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TransitionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TriggeredBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContourCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MonacoAckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FaultReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TimeoutTokenId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContouringSagaState", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContouringSagaState_CurrentState",
                table: "ContouringSagaState",
                column: "CurrentState");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContouringSagaState");
        }
    }
}
