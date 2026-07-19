using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextWord.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddArticleVocabPhoneticsAndExamples : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Phonetics",
            table: "ArticleVocabMappings",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ExamplesJson",
            table: "ArticleVocabMappings",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExamplesJson",
            table: "ArticleVocabMappings");

        migrationBuilder.DropColumn(
            name: "Phonetics",
            table: "ArticleVocabMappings");
    }
}
