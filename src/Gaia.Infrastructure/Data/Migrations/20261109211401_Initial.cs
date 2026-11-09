using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Gaia.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FieldALabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FieldBLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InputType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Formula = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                    table.CheckConstraint("CK_Operations_InputType", "[InputType] IN ('Number', 'Text', 'Coordinate')");
                    table.CheckConstraint("CK_Operations_Kind", "[Kind] IN ('Arithmetic', 'String', 'Other')");
                });

            migrationBuilder.InsertData(
                table: "Operations",
                columns: new[] { "Id", "CreatedAtUtc", "FieldALabel", "FieldBLabel", "Formula", "InputType", "IsDeleted", "IsEnabled", "IsSystem", "Kind", "Name", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { "add", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "First number", "Second number", "A + B", "Number", false, true, true, "Arithmetic", "Add", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "average", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "First number", "Second number", "(A + B) / 2", "Number", false, true, false, "Arithmetic", "Average", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "concat", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "First text", "Second text", "concat(A, B)", "Text", false, true, true, "String", "Concatenate", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "count", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Text", "Text to count", "count(A, B)", "Text", false, true, true, "String", "Count occurrences", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "divide", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Dividend", "Divisor", "A / B", "Number", false, true, true, "Arithmetic", "Divide", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "hypotenuse", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Side a", "Side b", "sqrt(A * A + B * B)", "Number", false, true, false, "Arithmetic", "Hypotenuse", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "multiply", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "First number", "Second number", "A * B", "Number", false, true, true, "Arithmetic", "Multiply", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "power", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Base", "Exponent", "A ^ B", "Number", false, true, true, "Arithmetic", "Power", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "subtract", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "First number", "Second number", "A - B", "Number", false, true, true, "Arithmetic", "Subtract", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "weather", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Latitude", "Longitude", null, "Coordinate", false, true, true, "Other", "Weather today", new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Operations");
        }
    }
}
