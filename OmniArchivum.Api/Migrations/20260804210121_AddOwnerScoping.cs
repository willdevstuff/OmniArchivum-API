using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniArchivum.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_Name",
                table: "Tags");

            migrationBuilder.AddColumn<string>(
                name: "OwnerKey",
                table: "Tags",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerKey",
                table: "Notes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_OwnerKey_Name",
                table: "Tags",
                columns: new[] { "OwnerKey", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_OwnerKey",
                table: "Notes",
                column: "OwnerKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_OwnerKey_Name",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Notes_OwnerKey",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "OwnerKey",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "OwnerKey",
                table: "Notes");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);
        }
    }
}
