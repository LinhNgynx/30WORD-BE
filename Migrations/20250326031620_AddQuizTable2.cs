using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeminiTest.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizTable2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OptionA",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "OptionB",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "OptionC",
                table: "Quizzes");

            migrationBuilder.RenameColumn(
                name: "OptionD",
                table: "Quizzes",
                newName: "OptionsJson");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Quizzes",
                type: "nvarchar(20)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Quizzes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Quizzes");

            migrationBuilder.RenameColumn(
                name: "OptionsJson",
                table: "Quizzes",
                newName: "OptionD");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Quizzes",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)");

            migrationBuilder.AddColumn<string>(
                name: "OptionA",
                table: "Quizzes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OptionB",
                table: "Quizzes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OptionC",
                table: "Quizzes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
