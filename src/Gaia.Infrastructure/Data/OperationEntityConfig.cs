using Gaia.Core.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gaia.Infrastructure.Data;

/// <summary>Maps <see cref="OperationDefinition"/> to the Operations table.</summary>
public class OperationEntityConfig : IEntityTypeConfiguration<OperationDefinition>
{
    public void Configure(EntityTypeBuilder<OperationDefinition> entity)
    {
        entity.ToTable("Operations");
        entity.HasKey(o => o.Id);

        entity.Property(o => o.Id).HasMaxLength(50);
        entity.Property(o => o.Name).HasMaxLength(100);
        entity.Property(o => o.FieldALabel).HasMaxLength(50);
        entity.Property(o => o.FieldBLabel).HasMaxLength(50);
        entity.Property(o => o.Formula).HasMaxLength(500);

        // Enums stored as their names ("Arithmetic", "Number") so the table is readable in SQL.
        entity.Property(o => o.Kind).HasConversion<string>().HasMaxLength(20);
        entity.Property(o => o.InputType).HasConversion<string>().HasMaxLength(20);

        // SQL Server rowversion: updated automatically on every save and used as the concurrency token.
        entity.Property(o => o.Version).IsRowVersion();

        entity.Ignore(o => o.IsAvailable);

        entity.HasData(SeedOperations.All);
    }
}
