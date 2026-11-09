using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PerfectDay_Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_completed",
                table: "tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_completed",
                table: "tasks");
        }
    }
}
