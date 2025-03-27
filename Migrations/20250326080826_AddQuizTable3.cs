using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeminiTest.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizTable3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "Wordlists",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "Quizzes",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Progress",
                table: "Wordlists");

            migrationBuilder.DropColumn(
                name: "IsCorrect",
                table: "Quizzes");
        }
    }
}
