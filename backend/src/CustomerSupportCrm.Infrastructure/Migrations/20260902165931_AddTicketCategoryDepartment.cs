using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomerSupportCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCategoryDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "TicketCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_DepartmentId",
                table: "TicketCategories",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketCategories_Departments_DepartmentId",
                table: "TicketCategories",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketCategories_Departments_DepartmentId",
                table: "TicketCategories");

            migrationBuilder.DropIndex(
                name: "IX_TicketCategories_DepartmentId",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "TicketCategories");
        }
    }
}
