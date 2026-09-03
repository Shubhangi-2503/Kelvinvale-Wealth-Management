using Kelvinvale.Domain.Entities;
using Kelvinvale.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Kelvinvale.Tests.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public static readonly Guid AdviserSarahId = DbInitializer.SampleAdviserId;
    public static readonly Guid CustomerAliceId = DbInitializer.SampleCustomerId;
    public static readonly Guid FundGlobalEquityId = DbInitializer.FundGlobalEquityId;
    public static readonly Guid FundUkBondId = DbInitializer.FundUkBondId;

    public static readonly Guid AliceIsaProductId = Guid.Parse("33333333-3333-3333-3333-aaaaaaaaaaaa");
    public static readonly Guid AliceSippProductId = Guid.Parse("55555555-5555-5555-5555-aaaaaaaaaaaa");
    public static readonly Guid CustomerCharlieId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 1. Tell ASP.NET Core this is the Testing environment
        builder.UseEnvironment("Testing");

        // 2. Seed data cleanly
        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KelvinvaleDbContext>();

            db.Database.EnsureCreated();
            DbInitializer.SeedAsync(db).GetAwaiter().GetResult();
            SeedTestScenarioData(db);
        });
    }

    private static void SeedTestScenarioData(KelvinvaleDbContext db)
    {
        if (!db.Users.Any(u => u.Id == CustomerCharlieId))
        {
            db.Users.Add(new User
            {
                Id = CustomerCharlieId,
                UserName = "charlie.customer",
                Email = "charlie.customer@example.com",
                DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                RoleId = DbInitializer.RoleCustomerId,
                IsActive = true,
                CreatedOn = DateTime.UtcNow
            });

            // Link Sarah as Charlie's authorized Adviser
            db.CustomerAdvisors.Add(new CustomerAdvisor
            {
                Id = Guid.NewGuid(),
                CustomerId = CustomerCharlieId,
                AdviserId = AdviserSarahId,
                IsActive = true,
                CreatedById = AdviserSarahId,
                CreatedOn = DateTime.UtcNow
            });
        }

        if (!db.Products.Any(p => p.Id == AliceIsaProductId))
        {
            db.Products.Add(new Product
            {
                Id = AliceIsaProductId,
                CustomerId = CustomerAliceId,
                ProductTypeId = DbInitializer.ProductTypeIsaId,
                TaxYear = 2026,
                IsActive = true,
                CreatedById = AdviserSarahId,
                CreatedOn = DateTime.UtcNow
            });

            db.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                ProductId = AliceIsaProductId,
                FundId = FundGlobalEquityId,
                AmountPence = 500000,
                IsActive = true,
                CreatedById = CustomerAliceId,
                CreatedOn = DateTime.UtcNow
            });
        }

        if (!db.Products.Any(p => p.Id == AliceSippProductId))
        {
            db.Products.Add(new Product
            {
                Id = AliceSippProductId,
                CustomerId = CustomerAliceId,
                ProductTypeId = DbInitializer.ProductTypeSippId,
                TaxYear = 2026,
                IsActive = true,
                CreatedById = AdviserSarahId,
                CreatedOn = DateTime.UtcNow
            });
        }

        db.SaveChanges();
    }
}