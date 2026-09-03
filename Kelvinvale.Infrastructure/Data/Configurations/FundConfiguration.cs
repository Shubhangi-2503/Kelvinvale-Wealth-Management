using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Data.Configurations
{
    public class FundConfiguration : IEntityTypeConfiguration<Fund>
    {
        public void Configure(EntityTypeBuilder<Fund> builder)
        {
            builder.ToTable("Funds");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.Code).IsRequired().HasMaxLength(50);
            builder.HasIndex(f => f.Code).IsUnique();
            builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        }
    }
}
