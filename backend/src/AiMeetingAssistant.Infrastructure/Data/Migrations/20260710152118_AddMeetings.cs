using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMeetingAssistant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OriginalTranscriptText = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SummaryText = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AssigneeHint = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    SuggestedForJira = table.Column<bool>(type: "boolean", nullable: false),
                    UserConfirmed = table.Column<bool>(type: "boolean", nullable: true),
                    SuggestedTicketTitle = table.Column<string>(type: "text", nullable: true),
                    SuggestedTicketDescription = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionItems_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KeyDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KeyDecisions_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_MeetingId",
                table: "ActionItems",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_KeyDecisions_MeetingId",
                table: "KeyDecisions",
                column: "MeetingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItems");

            migrationBuilder.DropTable(
                name: "KeyDecisions");

            migrationBuilder.DropTable(
                name: "Meetings");
        }
    }
}
