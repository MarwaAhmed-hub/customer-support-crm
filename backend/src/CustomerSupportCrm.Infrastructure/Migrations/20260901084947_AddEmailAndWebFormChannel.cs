using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailAndWebFormChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceChannel",
                table: "Tickets",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalMessageId",
                table: "CustomerInteractions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromAddress",
                table: "CustomerInteractions",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InReplyToMessageId",
                table: "CustomerInteractions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TicketId",
                table: "CustomerInteractions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToAddress",
                table: "CustomerInteractions",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_ExternalMessageId",
                table: "CustomerInteractions",
                column: "ExternalMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_TicketId",
                table: "CustomerInteractions",
                column: "TicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerInteractions_Tickets_TicketId",
                table: "CustomerInteractions",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerInteractions_Tickets_TicketId",
                table: "CustomerInteractions");

            migrationBuilder.DropIndex(
                name: "IX_CustomerInteractions_ExternalMessageId",
                table: "CustomerInteractions");

            migrationBuilder.DropIndex(
                name: "IX_CustomerInteractions_TicketId",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "SourceChannel",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ExternalMessageId",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "FromAddress",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "InReplyToMessageId",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "TicketId",
                table: "CustomerInteractions");

            migrationBuilder.DropColumn(
                name: "ToAddress",
                table: "CustomerInteractions");
        }
    }
}
