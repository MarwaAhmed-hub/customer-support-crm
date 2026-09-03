using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunicationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalConversationId",
                table: "Tickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SourceChannel_ExternalConversationId",
                table: "Tickets",
                columns: new[] { "SourceChannel", "ExternalConversationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_SourceChannel_ExternalConversationId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ExternalConversationId",
                table: "Tickets");
        }
    }
}
