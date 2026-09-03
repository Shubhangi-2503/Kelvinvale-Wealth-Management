using Kelvinvale.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kelvinvale.Infrastructure.Data;

public static class DbInitializer
{
    // Deterministic GUIDs for referencing across seeds and tests
    public static readonly Guid RoleAdviserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid RoleCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid ProductTypeIsaId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ProductTypeGiaId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid ProductTypeSippId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static readonly Guid InstructionTypeSubscriptionId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid InstructionTypeWithdrawalId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid InstructionTypeSwitchId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    // Pre-seeded Fund IDs
    public static readonly Guid FundGlobalEquityId = Guid.Parse("99999999-9999-9999-9999-999999999901");
    public static readonly Guid FundUkBondId = Guid.Parse("99999999-9999-9999-9999-999999999902");

    // Static constructor — valid place for statements at type scope
    static DbInitializer()
    {
        System.Console.WriteLine(FundGlobalEquityId);
        System.Console.WriteLine(FundUkBondId);
    }

    // Pre-seeded Adviser & Customer IDs for Postman testing
    public static readonly Guid SampleAdviserId = Guid.Parse("11111111-1111-1111-1111-111111111111"); // Sarah the Adviser
    public static readonly Guid SampleCustomerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"); // Alice the Customer

    public static async Task SeedAsync(KelvinvaleDbContext context)
    {
        var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // 1. Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new()
                {
                    Id = RoleAdviserId,
                    Name = "Adviser",
                    Description = "Wealth manager/adviser managing customer portfolios",
                    CreatedById = systemUserId,
                    CreatedOn = DateTime.UtcNow
                },
                new()
                {
                    Id = RoleCustomerId,
                    Name = "Customer",
                    Description = "Retail customer managing their personal products",
                    CreatedById = systemUserId,
                    CreatedOn = DateTime.UtcNow
                }
            };

            await context.Roles.AddRangeAsync(roles);
        }

        // 2. Seed Product Types
        if (!await context.ProductTypes.AnyAsync())
        {
            var productTypes = new List<ProductType>
            {
                new() { Id = ProductTypeIsaId, Code = "ISA", Name = "Stocks & Shares ISA", Description = "Tax-free individual savings account", CreatedById = systemUserId, CreatedOn = DateTime.UtcNow },
                new() { Id = ProductTypeGiaId, Code = "GIA", Name = "General Investment Account", Description = "Taxable investment account", CreatedById = systemUserId, CreatedOn = DateTime.UtcNow },
                new() { Id = ProductTypeSippId, Code = "SIPP", Name = "Self-Invested Personal Pension", Description = "Pension wrapper", CreatedById = systemUserId, CreatedOn = DateTime.UtcNow }
            };

            await context.ProductTypes.AddRangeAsync(productTypes);
        }

        // 3. Seed Instruction Types
        if (!await context.InstructionTypes.AnyAsync())
        {
            var instructionTypes = new List<InstructionType>
            {
                new() { Id = InstructionTypeSubscriptionId, Code = "SUBSCRIPTION", Name = "Subscription", Description = "Deposit funds", CreatedById = systemUserId, CreatedOn = DateTime.UtcNow },
                new() { Id = InstructionTypeWithdrawalId, Code = "WITHDRAWAL", Name = "Withdrawal", Description = "Withdraw funds", CreatedById = systemUserId, CreatedOn = DateTime.UtcNow },
                new() { Id = InstructionTypeSwitchId, Code = "SWITCH", Name = "Switch", Description = "Switch funds", CreatedById = systemUserId, CreatedOn = DateTime.UtcNow }
            };

            await context.InstructionTypes.AddRangeAsync(instructionTypes);
        }

        // 4. Seed Funds (Required for Instructions)
        if (!await context.Funds.AnyAsync())
        {
            var funds = new List<Fund>
            {
                new() { Id = FundGlobalEquityId, Code = "GLB-EQ-ACC", Name = "Global Equity Accumulation Fund", IsActive = true, CreatedById = systemUserId, CreatedOn = DateTime.UtcNow },
                new() { Id = FundUkBondId, Code = "UK-CORP-BND", Name = "UK Corporate Bond Index Fund", IsActive = true, CreatedById = systemUserId, CreatedOn = DateTime.UtcNow }
            };

            await context.Funds.AddRangeAsync(funds);
        }

        // 5. Seed Sample Users & Adviser Relationship (Adviser + Initial Customer)
        if (!await context.Users.AnyAsync(u => u.Id == SampleAdviserId))
        {
            var adviser = new User
            {
                Id = SampleAdviserId,
                UserName = "carole.adviser",
                Email = "carole.adviser@kelvinvale.com",
                RoleId = RoleAdviserId,
                IsActive = true,
                CreatedOn = DateTime.UtcNow
            };

            var customer = new User
            {
                Id = SampleCustomerId,
                UserName = "alice.customer",
                Email = "alice.customer@example.com",
                DateOfBirth = new DateTime(1994, 6, 12, 0, 0, 0, DateTimeKind.Utc),
                RoleId = RoleCustomerId,
                CreatedById = SampleAdviserId,
                IsActive = true,
                CreatedOn = DateTime.UtcNow
            };

            var relationship = new CustomerAdvisor
            {
                Id = Guid.NewGuid(),
                CustomerId = SampleCustomerId,
                AdviserId = SampleAdviserId,
                IsActive = true,
                CreatedById = SampleAdviserId,
                CreatedOn = DateTime.UtcNow
            };

            await context.Users.AddRangeAsync(adviser, customer);
            await context.CustomerAdvisors.AddAsync(relationship);
        }

        await context.SaveChangesAsync();
    }
}