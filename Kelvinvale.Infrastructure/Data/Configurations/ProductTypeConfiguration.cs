using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Data.Configurations
{
    public class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
    {
        public void Configure(EntityTypeBuilder<ProductType> builder)
        {
            builder.ToTable("ProductTypes");
            builder.HasKey(pt => pt.Id);

            builder.Property(pt => pt.Code).IsRequired().HasMaxLength(20);
            builder.HasIndex(pt => pt.Code).IsUnique();
            builder.Property(pt => pt.Name).IsRequired().HasMaxLength(100);
        }
    }
}
