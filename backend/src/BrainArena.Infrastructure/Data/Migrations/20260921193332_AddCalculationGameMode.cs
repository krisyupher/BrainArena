using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrainArena.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalculationGameMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string[]>(
                name: "Options",
                table: "Questions",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(string[]),
                oldType: "text[]");

            migrationBuilder.AlterColumn<int>(
                name: "CorrectOptionIndex",
                table: "Questions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "CorrectNumericAnswer",
                table: "Questions",
                type: "numeric",
                nullable: true);

            // Existing rows predate this column and are all multiple-choice — the enum's string
            // conversion serializes QuestionType.MultipleChoice as "MultipleChoice", not "", so the
            // default has to match that or every pre-existing question fails to deserialize.
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Questions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MultipleChoice");

            migrationBuilder.AddColumn<decimal>(
                name: "NumericAnswer",
                table: "MatchAnswers",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectNumericAnswer",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "NumericAnswer",
                table: "MatchAnswers");

            migrationBuilder.AlterColumn<string[]>(
                name: "Options",
                table: "Questions",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0],
                oldClrType: typeof(string[]),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CorrectOptionIndex",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
