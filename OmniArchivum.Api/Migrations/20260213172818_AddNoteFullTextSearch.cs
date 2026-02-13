using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace OmniArchivum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Notes",
                type: "tsvector",
                nullable: false)
                .Annotation("Npgsql:TsVectorConfig", "english")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Title", "BodyMarkdown" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_SearchVector",
                table: "Notes",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notes_SearchVector",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Notes");
        }
    }
}
