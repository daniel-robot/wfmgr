using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfmgr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseStatusOverlay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowCaseStatusOverlay",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowCaseStatusOverlay", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCaseStatusOverlay_Category",
                table: "WorkflowCaseStatusOverlay",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCaseStatusOverlay_SortOrder",
                table: "WorkflowCaseStatusOverlay",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowCaseStatusOverlay");
        }
    }
}
