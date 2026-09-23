using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrainArena.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Rooms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Multiplayer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Rooms");
        }
    }
}
