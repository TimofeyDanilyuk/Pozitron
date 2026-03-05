using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pozitron.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUnreadCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnreadCount",
                table: "ChatMembers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnreadCount",
                table: "ChatMembers");
        }
    }
}
