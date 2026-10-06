using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrainArena.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MarkExistingGuestUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guests created before the Guest role existed were stored as Player; tag them so the
            // RegisteredUser policy and GuestCleanup recognise them. No schema change — Role is a string column.
            migrationBuilder.Sql(
                """UPDATE "Users" SET "Role" = 'Guest' WHERE "Role" = 'Player' AND "Email" LIKE 'guest-%@guest.brainarena.local';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE "Users" SET "Role" = 'Player' WHERE "Role" = 'Guest';""");
        }
    }
}
