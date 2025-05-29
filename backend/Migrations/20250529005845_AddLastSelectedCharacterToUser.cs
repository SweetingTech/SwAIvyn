using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwAIvyn.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSelectedCharacterToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastSelectedCharacterId",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSelectedCharacterId",
                table: "Users");
        }
    }
}
