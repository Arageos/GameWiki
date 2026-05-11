using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameWiki.Migrations
{
    /// <inheritdoc />
    public partial class FavoriteListAndFavoriteGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "FavoriteLists",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteLists_UserId",
                table: "FavoriteLists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteGames_GameId",
                table: "FavoriteGames",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteGames_FavoriteLists_FavoriteListId",
                table: "FavoriteGames",
                column: "FavoriteListId",
                principalTable: "FavoriteLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteGames_Games_GameId",
                table: "FavoriteGames",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteLists_Users_UserId",
                table: "FavoriteLists",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteGames_FavoriteLists_FavoriteListId",
                table: "FavoriteGames");

            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteGames_Games_GameId",
                table: "FavoriteGames");

            migrationBuilder.DropForeignKey(
                name: "FK_FavoriteLists_Users_UserId",
                table: "FavoriteLists");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteLists_UserId",
                table: "FavoriteLists");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteGames_GameId",
                table: "FavoriteGames");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "FavoriteLists",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
