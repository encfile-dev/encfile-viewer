using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComfyBlazorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddEditQueueFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "BatchItems",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Generate");

            migrationBuilder.AddColumn<string>(
                name: "SourceImagePath",
                table: "BatchItems",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SourceImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    StoredFileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceImages_StoredFileName",
                table: "SourceImages",
                column: "StoredFileName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceImages");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "BatchItems");

            migrationBuilder.DropColumn(
                name: "SourceImagePath",
                table: "BatchItems");
        }
    }
}
