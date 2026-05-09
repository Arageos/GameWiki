using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameWiki.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundImageToGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundImage",
                table: "Games",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundImage",
                table: "Games");
        }
    }
}
