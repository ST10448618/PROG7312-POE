using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartX.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSensorFileClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "SensorFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "SensorFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "SensorFiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                table: "SensorFiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "SensorFiles");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "SensorFiles");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "SensorFiles");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "SensorFiles");
        }
    }
}
