using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Data.Configurations
{
    public class HoldingConfiguration : IEntityTypeConfiguration<Holding>
    {
        public void Configure(EntityTypeBuilder<Holding> builder)
        {
            builder.ToTable("Holdings", tb => tb.IsTemporal());
            builder.HasKey(h => h.Id);

            builder.Property(h => h.AmountPence).IsRequired();

            builder.HasOne(h => h.Fund)
                   .WithMany()
                   .HasForeignKey(h => h.FundId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(h => new { h.ProductId, h.FundId }).IsUnique();
        }
    }
}
