using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeminiTest.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizScoresToWordlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCorrect",
                table: "Quizzes");

            migrationBuilder.AddColumn<int>(
                name: "HighestContextScore",
                table: "Wordlists",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HighestMeaningScore",
                table: "Wordlists",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LatestContextScore",
                table: "Wordlists",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LatestMeaningScore",
                table: "Wordlists",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighestContextScore",
                table: "Wordlists");

            migrationBuilder.DropColumn(
                name: "HighestMeaningScore",
                table: "Wordlists");

            migrationBuilder.DropColumn(
                name: "LatestContextScore",
                table: "Wordlists");

            migrationBuilder.DropColumn(
                name: "LatestMeaningScore",
                table: "Wordlists");

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "Quizzes",
                type: "bit",
                nullable: true);
        }
    }
}
