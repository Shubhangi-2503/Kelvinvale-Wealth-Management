using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Data.Configurations
{
    public class InstructionConfiguration : IEntityTypeConfiguration<Instruction>
    {
        public void Configure(EntityTypeBuilder<Instruction> builder)
        {
            builder.ToTable("Instructions", tb => tb.IsTemporal());
            builder.HasKey(i => i.Id);

            builder.Property(i => i.AmountPence).IsRequired();
            builder.Property(i => i.ClientReference).IsRequired().HasMaxLength(100);
            builder.HasIndex(i => i.ClientReference).IsUnique();

            builder.Property(i => i.TargetFundCode).HasMaxLength(50);

            builder.HasOne(i => i.Product)
                   .WithMany()
                   .HasForeignKey(i => i.ProductId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(i => i.InstructionType)
                   .WithMany()
                   .HasForeignKey(i => i.InstructionTypeId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(i => i.Fund)
                   .WithMany()
                   .HasForeignKey(i => i.FundId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
