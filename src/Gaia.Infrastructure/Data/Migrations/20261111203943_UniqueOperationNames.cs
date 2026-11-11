using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gaia.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueOperationNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An existing database may already hold two live operations with the same name (A12). Keep the first
            // (by id) and give the others their id in brackets, so the unique index below can be created.
            migrationBuilder.Sql("""
                UPDATE o SET Name = LEFT(o.Name + N' (' + (o.Id COLLATE DATABASE_DEFAULT) + N')', 100)
                FROM Operations o
                WHERE o.IsDeleted = 0
                  AND EXISTS (SELECT 1 FROM Operations p WHERE p.IsDeleted = 0 AND p.Name = o.Name AND p.Id < o.Id);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Operations_Name",
                table: "Operations",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Operations_Name",
                table: "Operations");
        }
    }
}
