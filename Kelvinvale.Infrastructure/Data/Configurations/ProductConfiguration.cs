using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Data.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products", tb => tb.IsTemporal());
            builder.HasKey(p => p.Id);

            builder.HasOne(p => p.Customer)
                   .WithMany()
                   .HasForeignKey(p => p.CustomerId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.ProductType)
                   .WithMany()
                   .HasForeignKey(p => p.ProductTypeId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(p => p.Holdings)
                   .WithOne(h => h.Product)
                   .HasForeignKey(h => h.ProductId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
