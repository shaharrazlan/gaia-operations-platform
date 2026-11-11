using Gaia.Core.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gaia.Infrastructure.Data;

/// <summary>Maps <see cref="OperationDefinition"/> to the Operations table.</summary>
public class OperationEntityConfig : IEntityTypeConfiguration<OperationDefinition>
{
    public const string NameIndex = "IX_Operations_Name";

    public void Configure(EntityTypeBuilder<OperationDefinition> entity)
    {
        entity.ToTable("Operations", table =>
        {
            // The database itself refuses values the code can't read back.
            table.HasCheckConstraint("CK_Operations_Kind", InList("Kind", Enum.GetNames<OperationKind>()));
            table.HasCheckConstraint("CK_Operations_InputType", InList("InputType", Enum.GetNames<InputType>()));
        });
        entity.HasKey(o => o.Id);

        // Binary collation: ids are case-sensitive, so "ADD" is not "add" (SQL Server's default is case-insensitive).
        entity.Property(o => o.Id).HasMaxLength(50).UseCollation("Latin1_General_100_BIN2");
        entity.Property(o => o.Name).HasMaxLength(100);
        entity.Property(o => o.FieldALabel).HasMaxLength(50);
        entity.Property(o => o.FieldBLabel).HasMaxLength(50);
        entity.Property(o => o.Formula).HasMaxLength(500);

        // Enums stored as their names ("Arithmetic", "Number") so the table is readable in SQL.
        entity.Property(o => o.Kind).HasConversion<string>().HasMaxLength(20);
        entity.Property(o => o.InputType).HasConversion<string>().HasMaxLength(20);

        // SQL Server rowversion: updated automatically on every save and used as the concurrency token.
        entity.Property(o => o.Version).IsRowVersion();

        // Names are unique among live operations (R4.AC10): the service checks first; this index stops two
        // simultaneous saves. The column's default collation ignores case, like the service. Deleted rows don't count.
        entity.HasIndex(o => o.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName(NameIndex);

        entity.Ignore(o => o.IsAvailable);

        entity.HasData(SeedOperations.Create());
    }

    private static string InList(string column, IEnumerable<string> values) =>
        $"[{column}] IN ({string.Join(", ", values.Select(v => $"'{v}'"))})";
}
