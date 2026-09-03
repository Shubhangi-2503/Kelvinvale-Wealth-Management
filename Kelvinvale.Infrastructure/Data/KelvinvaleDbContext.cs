using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Kelvinvale.Infrastructure.Data
{
    public class KelvinvaleDbContext : DbContext
    {
        public KelvinvaleDbContext(DbContextOptions<KelvinvaleDbContext> options) : base(options)
        {

        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<CustomerAdvisor> CustomerAdvisors => Set<CustomerAdvisor>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductType> ProductTypes => Set<ProductType>();
        public DbSet<Fund> Funds => Set<Fund>();
        public DbSet<Holding> Holdings => Set<Holding>();
        public DbSet<Instruction> Instructions => Set<Instruction>();
        public DbSet<InstructionType> InstructionTypes => Set<InstructionType>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
