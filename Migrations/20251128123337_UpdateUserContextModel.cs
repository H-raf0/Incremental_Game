using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServerApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserContextModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BestScore",
                table: "Progressions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BestScore",
                table: "Progressions");
        }
    }
}
