using Kelvinvale.Api.Authentication;
using Kelvinvale.Api.Filters;
using Kelvinvale.Application.Interfaces;
using Kelvinvale.Application.Rules.Instruction;
using Kelvinvale.Application.Rules.Product;
using Kelvinvale.Infrastructure.Data;
using Kelvinvale.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// Configuring OpenAPI 
builder.Services.AddOpenApi();
// Add basic health check services
builder.Services.AddHealthChecks();

// Register Repository 
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductOpeningRule, IsaSingleAccountRule>();
builder.Services.AddScoped<IProductOpeningRule, SippAgeEligibilityRule>();
builder.Services.AddScoped<IInstructionRepository, InstructionRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<IIsaSubscriptionAllowanceRule, IsaSubscriptionAllowanceRule>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogActionFilter>();
});


builder.Services.AddDbContext<KelvinvaleDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        // Clean In-Memory registration with zero SQL Server services loaded
        options.UseInMemoryDatabase("Kelvinvale_TestingDb");
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions =>
        {
            sqlOptions.CommandTimeout(500); // Set default query timeout to 60s
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(100),
                errorNumbersToAdd: null);
        });
    }
});

// Register Authentication and set Default Scheme
builder.Services.AddAuthentication(HeaderAuthenticationOptions.SchemeName)
    .AddScheme<HeaderAuthenticationOptions, HeaderAuthenticationHandler>(
        HeaderAuthenticationOptions.SchemeName, null);

// Register Authorization
builder.Services.AddAuthorization();

var app = builder.Build();


//Seed database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<KelvinvaleDbContext>();

    // FIX: Only run migrations if using a relational provider (SQL Server)
    if (context.Database.IsRelational())
    {
        await context.Database.MigrateAsync();
    }

    // Seed data (ensure this doesn't call relational SQL directly)
    await DbInitializer.SeedAsync(context);
    
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");


app.Run();
