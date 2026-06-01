using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HappyPedia.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDownvotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Downvotes",
                table: "Articles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Downvotes",
                table: "Articles");
        }
    }
}
