using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiMeetingAssistant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActionItemDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "ActionItems",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "ActionItems");
        }
    }
}
