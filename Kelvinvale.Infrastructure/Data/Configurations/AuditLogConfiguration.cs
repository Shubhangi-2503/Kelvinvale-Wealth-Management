using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Data.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            // Table Mapping
            builder.ToTable("AuditLogs");

            // Primary Key
            builder.HasKey(a => a.Id);

            // Properties Constraints
            builder.Property(a => a.CallerId)
                .IsRequired();

            builder.Property(a => a.CallerRole)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(a => a.Action)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(a => a.EntityName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(a => a.EntityId)
                .IsRequired();

            builder.Property(a => a.CustomerId)
                .IsRequired(false);

            builder.Property(a => a.Details)
                .HasMaxLength(4000)
                .IsRequired(false);

            builder.Property(a => a.Timestamp)
                .IsRequired();

            builder.Property(a => a.IpAddress)
                .HasMaxLength(50)
                .IsRequired(false);

            // Performance Indexes for Compliance Querying
            builder.HasIndex(a => a.CustomerId)
                .HasDatabaseName("IX_AuditLogs_CustomerId");

            builder.HasIndex(a => a.Timestamp)
                .HasDatabaseName("IX_AuditLogs_TimestampUtc");

            builder.HasIndex(a => new { a.EntityName, a.EntityId })
                .HasDatabaseName("IX_AuditLogs_EntityName_EntityId");
        }
    }
}
