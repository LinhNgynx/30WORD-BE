using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeminiTest.Migrations
{
    /// <inheritdoc />
    public partial class wordlist2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExampleSentence",
                table: "Words",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Wordlists",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Wordlists",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExampleSentence",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Wordlists");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Wordlists");
        }
    }
}
