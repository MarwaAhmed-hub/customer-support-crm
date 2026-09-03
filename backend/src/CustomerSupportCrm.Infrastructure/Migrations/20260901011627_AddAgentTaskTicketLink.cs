using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTaskTicketLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TicketId",
                table: "AgentTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentTasks_TicketId",
                table: "AgentTasks",
                column: "TicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentTasks_Tickets_TicketId",
                table: "AgentTasks",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentTasks_Tickets_TicketId",
                table: "AgentTasks");

            migrationBuilder.DropIndex(
                name: "IX_AgentTasks_TicketId",
                table: "AgentTasks");

            migrationBuilder.DropColumn(
                name: "TicketId",
                table: "AgentTasks");
        }
    }
}
