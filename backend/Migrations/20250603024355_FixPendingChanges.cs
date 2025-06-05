using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwAIvyn.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Folders_FolderId",
                table: "Conversations");

            migrationBuilder.AddColumn<int>(
                name: "TargetStore",
                table: "Memories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Memories",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Folders_FolderId",
                table: "Conversations",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Folders_FolderId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "TargetStore",
                table: "Memories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Memories");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Folders_FolderId",
                table: "Conversations",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
