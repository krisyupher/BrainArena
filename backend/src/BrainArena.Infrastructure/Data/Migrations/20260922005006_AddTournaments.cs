using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrainArena.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Topic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GameMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    SecondsPerQuestion = table.Column<int>(type: "integer", nullable: false),
                    TournamentSize = table.Column<int>(type: "integer", nullable: false),
                    RoomSize = table.Column<int>(type: "integer", nullable: false),
                    AdvancesPerRoom = table.Column<int>(type: "integer", nullable: false),
                    MinPlayersToStart = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentRoundNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tournaments_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TournamentPlayers",
                columns: table => new
                {
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EliminatedAtRound = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentPlayers", x => new { x.TournamentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TournamentPlayers_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentPlayers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    IsFinal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentRounds_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentRoundRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentRoundRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentRoundRooms_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentRoundRooms_TournamentRounds_TournamentRoundId",
                        column: x => x.TournamentRoundId,
                        principalTable: "TournamentRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayers_UserId",
                table: "TournamentPlayers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRoundRooms_RoomId",
                table: "TournamentRoundRooms",
                column: "RoomId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRoundRooms_TournamentRoundId",
                table: "TournamentRoundRooms",
                column: "TournamentRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRounds_TournamentId",
                table: "TournamentRounds",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatorUserId",
                table: "Tournaments",
                column: "CreatorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentPlayers");

            migrationBuilder.DropTable(
                name: "TournamentRoundRooms");

            migrationBuilder.DropTable(
                name: "TournamentRounds");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
