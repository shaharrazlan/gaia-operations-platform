using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gaia.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_BIN2"),
                    FieldA = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FieldB = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Result = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationRuns_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationRuns_History",
                table: "OperationRuns",
                columns: new[] { "OperationId", "IsSuccess", "CreatedAtUtc" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationRuns");
        }
    }
}
