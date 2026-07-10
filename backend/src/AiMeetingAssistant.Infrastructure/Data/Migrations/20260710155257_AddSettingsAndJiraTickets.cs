using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMeetingAssistant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsAndJiraTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaudeApiKey = table.Column<string>(type: "text", nullable: true),
                    JiraBaseUrl = table.Column<string>(type: "text", nullable: true),
                    JiraEmail = table.Column<string>(type: "text", nullable: true),
                    JiraApiToken = table.Column<string>(type: "text", nullable: true),
                    JiraDefaultProjectKey = table.Column<string>(type: "text", nullable: true),
                    JiraDefaultIssueType = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JiraTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    JiraIssueKey = table.Column<string>(type: "text", nullable: true),
                    JiraIssueUrl = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JiraTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JiraTickets_ActionItems_ActionItemId",
                        column: x => x.ActionItemId,
                        principalTable: "ActionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_UserId",
                table: "AppSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JiraTickets_ActionItemId",
                table: "JiraTickets",
                column: "ActionItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "JiraTickets");
        }
    }
}
