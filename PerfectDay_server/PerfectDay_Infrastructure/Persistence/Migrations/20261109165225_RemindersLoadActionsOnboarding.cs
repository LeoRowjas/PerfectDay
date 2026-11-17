using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PerfectDay_Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemindersLoadActionsOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "browser_notifications",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "onboarded_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "remind_before_task",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "rest_reminders",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "dismissed_warnings",
                table: "day_schedules",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<bool>(
                name: "is_lightened",
                table: "day_schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "notification_reads",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_reads", x => new { x.user_id, x.key });
                    table.ForeignKey(
                        name: "fk_notification_reads_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Кто уже строил расписание, прошёл быстрый старт.
            migrationBuilder.Sql(
                "UPDATE users SET onboarded_at = created_at WHERE EXISTS (SELECT 1 FROM day_schedules s WHERE s.user_id = users.id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_reads");

            migrationBuilder.DropColumn(
                name: "browser_notifications",
                table: "users");

            migrationBuilder.DropColumn(
                name: "onboarded_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "remind_before_task",
                table: "users");

            migrationBuilder.DropColumn(
                name: "rest_reminders",
                table: "users");

            migrationBuilder.DropColumn(
                name: "dismissed_warnings",
                table: "day_schedules");

            migrationBuilder.DropColumn(
                name: "is_lightened",
                table: "day_schedules");
        }
    }
}
