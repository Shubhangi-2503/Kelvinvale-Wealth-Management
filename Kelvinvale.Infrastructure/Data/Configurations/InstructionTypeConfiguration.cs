using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Data.Configurations
{
    public class InstructionTypeConfiguration : IEntityTypeConfiguration<InstructionType>
    {
        public void Configure(EntityTypeBuilder<InstructionType> builder)
        {
            builder.ToTable("InstructionTypes");
            builder.HasKey(it => it.Id);

            builder.Property(it => it.Code).IsRequired().HasMaxLength(50);
            builder.HasIndex(it => it.Code).IsUnique();
            builder.Property(it => it.Name).IsRequired().HasMaxLength(100);
        }
    }
}
