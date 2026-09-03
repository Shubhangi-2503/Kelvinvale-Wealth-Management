using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Data.Configurations
{
    public class CustomerAdvisorConfiguration : IEntityTypeConfiguration<CustomerAdvisor>
    {
        public void Configure(EntityTypeBuilder<CustomerAdvisor> builder)
        {
            builder.ToTable("CustomerAdvisors", tb => tb.IsTemporal());
            builder.HasKey(ca => ca.Id);

            builder.HasOne(ca => ca.Customer)
                   .WithMany()
                   .HasForeignKey(ca => ca.CustomerId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(ca => ca.Adviser)
                   .WithMany()
                   .HasForeignKey(ca => ca.AdviserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(ca => new { ca.CustomerId, ca.AdviserId });
        }
    }
}
