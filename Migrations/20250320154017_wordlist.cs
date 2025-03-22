using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeminiTest.Migrations
{
    /// <inheritdoc />
    public partial class wordlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Wordlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wordlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wordlists_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Words",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WordText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phonetic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PartOfSpeech = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnglishMeaning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VietnameseMeaning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WordlistId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Words", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Words_Wordlists_WordlistId",
                        column: x => x.WordlistId,
                        principalTable: "Wordlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Wordlists_UserId",
                table: "Wordlists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Words_WordlistId",
                table: "Words",
                column: "WordlistId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Words");

            migrationBuilder.DropTable(
                name: "Wordlists");
        }
    }
}
