using Gaia.Core.History;
using Gaia.Core.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gaia.Infrastructure.Data;

/// <summary>Maps <see cref="OperationRun"/> to the OperationRuns table (R7).</summary>
public class OperationRunEntityConfig : IEntityTypeConfiguration<OperationRun>
{
    public void Configure(EntityTypeBuilder<OperationRun> entity)
    {
        entity.ToTable("OperationRuns");
        entity.HasKey(r => r.Id);
        entity.Property(r => r.Id).UseIdentityColumn();

        // Same case-sensitive collation as Operations.Id, so the foreign key matches exactly.
        entity.Property(r => r.OperationId).HasMaxLength(50).UseCollation("Latin1_General_100_BIN2");
        entity.Property(r => r.FieldA).HasMaxLength(OperationRun.MaxFieldLength);
        entity.Property(r => r.FieldB).HasMaxLength(OperationRun.MaxFieldLength);
        entity.Property(r => r.Result).HasMaxLength(OperationRun.MaxResultLength);
        entity.Property(r => r.ErrorCode).HasMaxLength(50);
        entity.Property(r => r.ErrorMessage).HasMaxLength(OperationRun.MaxErrorMessageLength);

        // Every run belongs to an operation. Operations are only soft-deleted, so Restrict never blocks anything,
        // and it keeps history from being wiped by a hard delete done by hand.
        entity.HasOne<OperationDefinition>()
            .WithMany()
            .HasForeignKey(r => r.OperationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Serves both history questions: "latest successful runs of X" and "successful runs of X since <date>".
        entity.HasIndex(r => new { r.OperationId, r.IsSuccess, r.CreatedAtUtc })
            .HasDatabaseName("IX_OperationRuns_History")
            .IsDescending(false, false, true);
    }
}
