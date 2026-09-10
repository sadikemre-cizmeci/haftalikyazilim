using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaftalikYazilim.Web.Migrations
{
    /// <inheritdoc />
    public partial class LikedListIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArticleLikes_UserId",
                table: "ArticleLikes");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLikes_UserId_CreatedAtUtc",
                table: "ArticleLikes",
                columns: new[] { "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArticleLikes_UserId_CreatedAtUtc",
                table: "ArticleLikes");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLikes_UserId",
                table: "ArticleLikes",
                column: "UserId");
        }
    }
}
